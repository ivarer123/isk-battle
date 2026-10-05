using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Auth;

/// <summary>
/// Local development sign-in without Entra: the web app sends a player name in <c>X-Dev-User</c>.
/// Only registered when <c>Auth:DevMode</c> is true, which Program refuses outside Development.
/// </summary>
internal sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";
    public const string Header = "X-Dev-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Headers[Header].ToString().Trim().ToLowerInvariant();
        if (name.Length == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim(CurrentUser.IdClaim, $"dev|{name}"), new Claim("email", $"{name}@dev.local")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
