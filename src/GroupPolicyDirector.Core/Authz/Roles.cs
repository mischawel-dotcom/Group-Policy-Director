namespace GroupPolicyDirector.Core.Authz;

/// <summary>
/// Global product roles (aligned with Referenz-Tools). Assigned in-tool; trustees are AD groups/users.
/// </summary>
public enum ProductRole
{
    ProductAdministrator = 1,
    GpoCreator = 2,
    BreakGlass = 3,
    Auditor = 4
}

/// <summary>
/// Object-level roles on OU/container or individual GPO scope.
/// </summary>
public enum ObjectRole
{
    Editor = 1,
    Approver = 2,
    Deployer = 3
}

/// <summary>
/// Scope for a delegation grant.
/// </summary>
public enum DelegationScopeKind
{
    Ou = 1,
    Gpo = 2,
    /// <summary>AD Site / Domain / OU container under link change-control.</summary>
    Container = 3
}
