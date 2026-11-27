using System.Text.Json;
using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Builds a GPMC-familiar Computer/User → Policies/Preferences tree.
/// Editable Admin Template samples are real in-memory settings; deeper ADMX areas are stubs.
/// </summary>
public static class PolicyTreeFactory
{
    public static PolicyTreeNode CreateBaseline(string displayName) =>
        new()
        {
            Path = "/",
            Name = displayName,
            Children =
            [
                new PolicyTreeNode
                {
                    Path = "/Computer",
                    Name = "Computer Configuration",
                    Children =
                    [
                        new PolicyTreeNode
                        {
                            Path = "/Computer/Policies",
                            Name = "Policies",
                            Children =
                            [
                                new PolicyTreeNode
                                {
                                    Path = "/Computer/Policies/Administrative Templates",
                                    Name = "Administrative Templates",
                                    Children =
                                    [
                                        new PolicyTreeNode
                                        {
                                            Path = "/Computer/Policies/Administrative Templates/Windows Components",
                                            Name = "Windows Components",
                                            Children =
                                            [
                                                new PolicyTreeNode
                                                {
                                                    Path = "/Computer/Policies/Administrative Templates/Windows Components/Windows Update",
                                                    Name = "Windows Update",
                                                    Children =
                                                    [
                                                        Leaf(
                                                            "/Computer/Policies/Administrative Templates/Windows Components/Windows Update/Configure Automatic Updates",
                                                            "Configure Automatic Updates",
                                                            "NotConfigured",
                                                            settingType: "PolicyState",
                                                            editable: true),
                                                        Leaf(
                                                            "/Computer/Policies/Administrative Templates/Windows Components/Windows Update/Specify intranet Microsoft update service location",
                                                            "Specify intranet Microsoft update service location",
                                                            "NotConfigured",
                                                            value: "",
                                                            settingType: "Text",
                                                            editable: true)
                                                    ]
                                                },
                                                new PolicyTreeNode
                                                {
                                                    Path = "/Computer/Policies/Administrative Templates/Windows Components/Windows Defender Antivirus",
                                                    Name = "Microsoft Defender Antivirus",
                                                    Children =
                                                    [
                                                        Leaf(
                                                            "/Computer/Policies/Administrative Templates/Windows Components/Windows Defender Antivirus/Turn off Microsoft Defender Antivirus",
                                                            "Turn off Microsoft Defender Antivirus",
                                                            "Disabled",
                                                            settingType: "PolicyState",
                                                            editable: true)
                                                    ]
                                                }
                                            ]
                                        },
                                        new PolicyTreeNode
                                        {
                                            Path = "/Computer/Policies/Administrative Templates/System",
                                            Name = "System",
                                            Children =
                                            [
                                                Leaf(
                                                    "/Computer/Policies/Administrative Templates/System/Do not display the Getting Started welcome screen at logon",
                                                    "Do not display the Getting Started welcome screen at logon",
                                                    "Enabled",
                                                    settingType: "PolicyState",
                                                    editable: true)
                                            ]
                                        }
                                    ]
                                },
                                new PolicyTreeNode
                                {
                                    Path = "/Computer/Policies/Windows Settings",
                                    Name = "Windows Settings",
                                    Children =
                                    [
                                        new PolicyTreeNode
                                        {
                                            Path = "/Computer/Policies/Windows Settings/Security Settings",
                                            Name = "Security Settings",
                                            Children =
                                            [
                                                Leaf(
                                                    "/Computer/Policies/Windows Settings/Security Settings/Account Policies",
                                                    "Account Policies",
                                                    "Stub",
                                                    value: "Requires GPMC/ADMX provider",
                                                    editable: false,
                                                    stub: true)
                                            ]
                                        }
                                    ]
                                }
                            ]
                        },
                        new PolicyTreeNode
                        {
                            Path = "/Computer/Preferences",
                            Name = "Preferences",
                            Children =
                            [
                                Leaf(
                                    "/Computer/Preferences/Control Panel Settings",
                                    "Control Panel Settings",
                                    "Stub",
                                    value: "Preferences editor not available without GPMC agent",
                                    editable: false,
                                    stub: true)
                            ]
                        }
                    ]
                },
                new PolicyTreeNode
                {
                    Path = "/User",
                    Name = "User Configuration",
                    Children =
                    [
                        new PolicyTreeNode
                        {
                            Path = "/User/Policies",
                            Name = "Policies",
                            Children =
                            [
                                new PolicyTreeNode
                                {
                                    Path = "/User/Policies/Administrative Templates",
                                    Name = "Administrative Templates",
                                    Children =
                                    [
                                        new PolicyTreeNode
                                        {
                                            Path = "/User/Policies/Administrative Templates/Start Menu and Taskbar",
                                            Name = "Start Menu and Taskbar",
                                            Children =
                                            [
                                                Leaf(
                                                    "/User/Policies/Administrative Templates/Start Menu and Taskbar/Remove Recycle Bin icon from desktop",
                                                    "Remove Recycle Bin icon from desktop",
                                                    "NotConfigured",
                                                    settingType: "PolicyState",
                                                    editable: true)
                                            ]
                                        }
                                    ]
                                }
                            ]
                        },
                        new PolicyTreeNode
                        {
                            Path = "/User/Preferences",
                            Name = "Preferences",
                            Children =
                            [
                                Leaf(
                                    "/User/Preferences/Windows Settings",
                                    "Windows Settings",
                                    "Stub",
                                    value: "Preferences editor not available without GPMC agent",
                                    editable: false,
                                    stub: true)
                            ]
                        }
                    ]
                }
            ]
        };

    public static PolicyTreeNode Clone(PolicyTreeNode node)
    {
        var json = JsonSerializer.Serialize(node);
        return JsonSerializer.Deserialize<PolicyTreeNode>(json)
               ?? throw new InvalidOperationException("Failed to clone policy tree.");
    }

    public static bool TryUpdate(PolicyTreeNode root, PolicySettingUpdate update)
    {
        var target = Find(root, update.Path);
        if (target is null)
        {
            return false;
        }

        if (!target.IsEditable || target.IsStub)
        {
            throw new InvalidOperationException($"Setting '{update.Path}' is not editable (stub or folder).");
        }

        if (update.State is not null)
        {
            var allowed = new[] { "Enabled", "Disabled", "NotConfigured" };
            if (target.SettingType == "PolicyState" &&
                !allowed.Contains(update.State, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Policy state must be Enabled, Disabled, or NotConfigured.");
            }

            target.State = update.State;
        }

        if (update.Value is not null)
        {
            target.Value = update.Value;
        }

        return true;
    }

    public static PolicyTreeNode? Find(PolicyTreeNode node, string path)
    {
        if (node.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var hit = Find(child, path);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>Leaf paths whose State/Value differ between baseline and working trees.</summary>
    public static IReadOnlyList<string> CollectChangedPaths(PolicyTreeNode baseline, PolicyTreeNode working)
    {
        var before = new Dictionary<string, (string? State, string? Value)>(StringComparer.OrdinalIgnoreCase);
        var after = new Dictionary<string, (string? State, string? Value)>(StringComparer.OrdinalIgnoreCase);
        CollectLeaves(baseline, before);
        CollectLeaves(working, after);

        var changed = new List<string>();
        foreach (var (path, value) in after)
        {
            if (!before.TryGetValue(path, out var prior) ||
                !string.Equals(prior.State, value.State, StringComparison.Ordinal) ||
                !string.Equals(prior.Value, value.Value, StringComparison.Ordinal))
            {
                changed.Add(path);
            }
        }

        foreach (var path in before.Keys)
        {
            if (!after.ContainsKey(path))
            {
                changed.Add(path);
            }
        }

        return changed;
    }

    private static void CollectLeaves(
        PolicyTreeNode node,
        IDictionary<string, (string? State, string? Value)> bag)
    {
        if (node.Children.Count == 0 && (node.State is not null || node.Value is not null || node.IsEditable))
        {
            bag[node.Path] = (node.State, node.Value);
        }

        foreach (var child in node.Children)
        {
            CollectLeaves(child, bag);
        }
    }

    private static PolicyTreeNode Leaf(
        string path,
        string name,
        string state,
        string? value = null,
        string? settingType = null,
        bool editable = false,
        bool stub = false) =>
        new()
        {
            Path = path,
            Name = name,
            State = state,
            Value = value,
            SettingType = settingType,
            IsEditable = editable,
            IsStub = stub,
            Children = []
        };
}
