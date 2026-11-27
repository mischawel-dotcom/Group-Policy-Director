using System.Security.Claims;
using System.Text.Encodings.Web;
using GroupPolicyDirector.Infrastructure;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGpdInfrastructure();

builder.Services.AddCors(options =>
{
    options.AddPolicy("UiDev", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var useNegotiate = builder.Configuration.GetValue("Auth:UseNegotiate", false);
if (useNegotiate)
{
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
        .AddNegotiate();
}
else
{
    // Development stub: always authenticate as bootstrap Product Admin.
    builder.Services.AddAuthentication("GpdDevStub")
        .AddScheme<AuthenticationSchemeOptions, GpdDevAuthHandler>("GpdDevStub", _ => { });
}

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("UiDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Development authentication handler when Negotiate is unavailable.</summary>
file sealed class GpdDevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, WindowsAuthPlaceholders.DevProductAdmin),
                new Claim(ClaimTypes.Role, "ProductAdministrator"),
                new Claim(WindowsAuthPlaceholders.AuthModeClaim, WindowsAuthPlaceholders.DevelopmentStubMode)
            ],
            authenticationType: Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
