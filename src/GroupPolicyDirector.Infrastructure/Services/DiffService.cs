using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

public sealed class DiffService(GpdStore store) : IDiffService
{
    public Task<VersionDiffResult> DiffGpoVersionsAsync(
        Guid controlledGpoId,
        int fromVersion,
        int toVersion,
        CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            var gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                      ?? throw new KeyNotFoundException("Controlled GPO not found.");

            var from = store.Versions.FirstOrDefault(v =>
                           v.ControlledGpoId == controlledGpoId && v.VersionNumber == fromVersion)
                       ?? throw new KeyNotFoundException($"Version {fromVersion} not found.");
            var to = store.Versions.FirstOrDefault(v =>
                         v.ControlledGpoId == controlledGpoId && v.VersionNumber == toVersion)
                     ?? throw new KeyNotFoundException($"Version {toVersion} not found.");

            return Task.FromResult(DiffSnapshots(
                gpo.ProductionGpoId,
                "Gpo",
                fromVersion,
                toVersion,
                from.SettingsSnapshotJson,
                to.SettingsSnapshotJson,
                linksMode: false));
        }
    }

    public VersionDiffResult DiffSnapshots(
        string objectKey,
        string objectType,
        int fromVersion,
        int toVersion,
        string? beforeJson,
        string? afterJson,
        bool linksMode)
    {
        Dictionary<string, string> before;
        Dictionary<string, string> after;

        if (linksMode)
        {
            before = FlattenLinks(beforeJson);
            after = FlattenLinks(afterJson);
        }
        else
        {
            before = FlattenTree(beforeJson);
            after = FlattenTree(afterJson);
        }

        var changes = new List<DiffChange>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var hasBefore = before.TryGetValue(key, out var b);
            var hasAfter = after.TryGetValue(key, out var a);
            if (hasBefore && !hasAfter)
            {
                changes.Add(new DiffChange { Path = key, Kind = "Removed", Before = b, After = null });
            }
            else if (!hasBefore && hasAfter)
            {
                changes.Add(new DiffChange { Path = key, Kind = "Added", Before = null, After = a });
            }
            else if (hasBefore && hasAfter && !string.Equals(b, a, StringComparison.Ordinal))
            {
                changes.Add(new DiffChange { Path = key, Kind = "Changed", Before = b, After = a });
            }
        }

        return new VersionDiffResult
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            ObjectKey = objectKey,
            ObjectType = objectType,
            Changes = changes
        };
    }

    private static Dictionary<string, string> FlattenTree(string? json)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            var root = JsonSerializer.Deserialize<PolicyTreeNode>(json);
            if (root is not null)
            {
                Walk(root, map);
            }
        }
        catch (JsonException)
        {
            map["(raw)"] = Truncate(json);
        }

        return map;
    }

    private static void Walk(PolicyTreeNode node, Dictionary<string, string> map)
    {
        if (node.IsEditable || node.State is not null || node.Value is not null)
        {
            map[node.Path] = FormatSetting(node.State, node.Value);
        }

        foreach (var child in node.Children)
        {
            Walk(child, map);
        }
    }

    private static Dictionary<string, string> FlattenLinks(string? json)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            var state = JsonSerializer.Deserialize<ContainerLinksState>(json);
            if (state is null)
            {
                return map;
            }

            map["BlockInheritance"] = state.BlockInheritance ? "true" : "false";
            foreach (var link in state.Links.OrderBy(l => l.Order))
            {
                var label = string.IsNullOrWhiteSpace(link.DisplayName) ? link.GpoId : $"{link.DisplayName} ({link.GpoId})";
                map[$"Link[{link.Order}].{link.GpoId}"] =
                    $"{label}; enforced={link.Enforced}; disabled={link.Disabled}";
            }
        }
        catch (JsonException)
        {
            map["(raw)"] = Truncate(json);
        }

        return map;
    }

    private static string FormatSetting(string? state, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(state))
        {
            return $"{state} = {value}";
        }

        return value ?? state ?? "(empty)";
    }

    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..200] + "…";
}
