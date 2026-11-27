namespace GroupPolicyDirector.Infrastructure.Auth;

/// <summary>
/// Windows Integrated Auth placeholders. Production: Negotiate/Kerberos against domain.
/// Development: synthetic Product Admin when no Windows identity is present.
/// </summary>
public static class WindowsAuthPlaceholders
{
    public const string DevProductAdmin = @"CONTOSO\gpd-admin";
    public const string AuthModeClaim = "gpd_auth_mode";
    public const string DevelopmentStubMode = "development-stub";
    public const string NegotiateMode = "windows-negotiate";
}
