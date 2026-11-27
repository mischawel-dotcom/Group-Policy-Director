using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GroupPolicyDirector.Core.Agpm;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Stub parser for AGPM-style history archives.
/// Accepts either a directory tree or a .zip with GUID folders and optional Backup.xml / manifest.json.
/// Not a full reverse-engineered AGPM binary format — fixture-compatible layout for dry-run demos.
/// </summary>
public static partial class AgpmArchiveParser
{
    private static readonly Regex GuidFolder = GuidFolderRegex();

    public static async Task<ParsedArchive> ParseAsync(
        string? archivePath,
        byte[]? zipBytes,
        string? zipFileName,
        CancellationToken ct)
    {
        if (zipBytes is { Length: > 0 })
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "gpd-agpm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                await using (var ms = new MemoryStream(zipBytes))
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false))
                {
                    zip.ExtractToDirectory(tempRoot);
                }

                var label = zipFileName ?? "(uploaded.zip)";
                var entries = ParseDirectory(tempRoot);
                return new ParsedArchive(label, 1, entries, $"Parsed uploaded zip '{label}' ({zipBytes.Length} bytes).");
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { /* best-effort */ }
            }
        }

        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return new ParsedArchive("(none)", 0, [], "Archive path is empty and no zip upload was provided.");
        }

        if (File.Exists(archivePath) &&
            archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(archivePath, ct);
            return await ParseAsync(null, bytes, Path.GetFileName(archivePath), ct);
        }

        if (Directory.Exists(archivePath))
        {
            var entries = ParseDirectory(archivePath);
            return new ParsedArchive(archivePath, 1, entries, $"Parsed directory archive at '{archivePath}'.");
        }

        return new ParsedArchive(
            archivePath,
            0,
            [],
            $"Path not found: {archivePath}. Provide an AGPM history directory, .zip, or upload a fixture.");
    }

    private static List<RawGpoFolder> ParseDirectory(string root)
    {
        var results = new List<RawGpoFolder>();

        // Prefer an explicit manifest when present (fixture / future exporter).
        var manifestPath = Path.Combine(root, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (doc.RootElement.TryGetProperty("gpos", out var gpos) && gpos.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in gpos.EnumerateArray())
                    {
                        var id = g.TryGetProperty("gpoId", out var idEl) ? idEl.GetString() : null;
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        var name = g.TryGetProperty("displayName", out var nEl) ? nEl.GetString() : null;
                        var versions = new List<string>();
                        if (g.TryGetProperty("versions", out var vEl) && vEl.ValueKind == JsonValueKind.Array)
                        {
                            versions.AddRange(vEl.EnumerateArray()
                                .Select(x => x.ValueKind == JsonValueKind.String
                                    ? x.GetString() ?? ""
                                    : x.ToString())
                                .Where(s => !string.IsNullOrWhiteSpace(s)));
                        }

                        results.Add(new RawGpoFolder(NormalizeGuid(id), name, versions));
                    }

                    if (results.Count > 0)
                    {
                        return results;
                    }
                }
            }
            catch
            {
                // Fall through to folder scan.
            }
        }

        // Scan for GUID-named folders (AGPM-style history layout).
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(dir);
            if (!GuidFolder.IsMatch(name))
            {
                // Also allow nested History/{guid}
                foreach (var nested in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    var nestedName = Path.GetFileName(nested);
                    if (GuidFolder.IsMatch(nestedName))
                    {
                        results.Add(ParseGpoFolder(nested, nestedName));
                    }
                }

                continue;
            }

            results.Add(ParseGpoFolder(dir, name));
        }

        return results;
    }

    private static RawGpoFolder ParseGpoFolder(string dir, string folderName)
    {
        var gpoId = NormalizeGuid(folderName);
        string? displayName = null;
        var versionLabels = new List<string>();

        var backupXml = Directory.EnumerateFiles(dir, "Backup.xml", SearchOption.AllDirectories).FirstOrDefault();
        if (backupXml is not null)
        {
            try
            {
                var x = XDocument.Load(backupXml);
                displayName =
                    x.Descendants().FirstOrDefault(e => e.Name.LocalName is "DisplayName" or "GPODisplayName")?.Value
                    ?? displayName;
                var idNode = x.Descendants().FirstOrDefault(e => e.Name.LocalName is "Id" or "GPOId" or "Guid");
                if (idNode is not null && !string.IsNullOrWhiteSpace(idNode.Value))
                {
                    gpoId = NormalizeGuid(idNode.Value);
                }
            }
            catch
            {
                // Ignore malformed Backup.xml in stub parser.
            }
        }

        // Version folders: numeric or vN under the GPO root.
        foreach (var versionDir in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var label = Path.GetFileName(versionDir);
            if (int.TryParse(label, out _) || label.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionLabels.Add(label);
            }
        }

        if (versionLabels.Count == 0)
        {
            // Single-shot backup without version subfolders still counts as one version.
            if (Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any())
            {
                versionLabels.Add("1");
            }
        }

        return new RawGpoFolder(gpoId, displayName, versionLabels);
    }

    private static string NormalizeGuid(string raw)
    {
        var trimmed = raw.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var g) ? g.ToString("B").ToUpperInvariant() : raw.Trim();
    }

    public sealed record RawGpoFolder(string GpoId, string? DisplayName, IReadOnlyList<string> VersionLabels);

    public sealed record ParsedArchive(
        string ArchivePath,
        int ArchivesScanned,
        IReadOnlyList<RawGpoFolder> Folders,
        string Notes);

    [GeneratedRegex(@"^\{?[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}\}?$", RegexOptions.CultureInvariant)]
    private static partial Regex GuidFolderRegex();
}
