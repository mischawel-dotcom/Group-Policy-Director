namespace GroupPolicyDirector.Infrastructure.Auth;

/// <summary>
/// Windows Integrated Auth placeholders. Production: Negotiate/Kerberos against domain.
/// Development: synthetic identities + AD admin-group trustee stubs when no domain is present.
/// </summary>
public static class WindowsAuthPlaceholders
{
    public const string DevProductAdmin = @"CONTOSO\gpd-admin";
    public const string DevEditor = @"CONTOSO\gpd-editor";
    public const string DevApprover = @"CONTOSO\gpd-approver";
    public const string DevDeployer = @"CONTOSO\gpd-deployer";

    public const string EditorsGroup = @"CONTOSO\GPD-Editors";
    public const string ApproversGroup = @"CONTOSO\GPD-Approvers";
    public const string DeployersGroup = @"CONTOSO\GPD-Deployers";
    public const string ProductAdminsGroup = @"CONTOSO\GPD-ProductAdmins";

    public const string AuthModeClaim = "gpd_auth_mode";
    public const string DevelopmentStubMode = "development-stub";
    public const string NegotiateMode = "windows-negotiate";

    /// <summary>Optional header (dev stub only) to impersonate another demo principal for SoD demos.</summary>
    public const string ActorHeader = "X-Gpd-Actor";

    public static readonly IReadOnlyList<string> DemoActors =
    [
        DevProductAdmin,
        DevEditor,
        DevApprover,
        DevDeployer
    ];
}
