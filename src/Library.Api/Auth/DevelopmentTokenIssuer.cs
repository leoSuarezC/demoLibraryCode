using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Library.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Library.Api.Auth;

/// <summary>An account offered on the demo sign-in screen.</summary>
public record DemoAccount(
    string Id,
    string Name,
    string Email,
    string Role,
    Guid? MemberId,
    string Description);

/// <summary>
/// Issues signed JWTs for the seeded demo accounts so the system can be run and
/// reviewed without registering with an identity provider.
/// </summary>
/// <remarks>
/// The tokens are ordinary HS256 JWTs carrying the same claims a real provider would
/// send, which is the point: the rest of the API cannot tell the difference, so
/// swapping in Auth0 or Entra changes configuration and nothing else.
///
/// This is refused outright whenever an external authority is configured - see
/// <see cref="AuthOptions.UsesExternalProvider"/>. It verifies no credential, because
/// there is none to verify.
/// </remarks>
public static class DevelopmentTokenIssuer
{
    /// <summary>Staff accounts. Members come from the seeded catalogue instead.</summary>
    public static readonly DemoAccount[] StaffAccounts =
    [
        new("staff-admin", "Amara Okafor", "amara.okafor@athenaeum.demo", LibraryRole.Admin,
            null, "Full access, including removing titles from the catalogue."),
        new("staff-librarian", "Sam Reyes", "sam.reyes@athenaeum.demo", LibraryRole.Librarian,
            null, "Works the desk: catalogues, lends and takes returns.")
    ];

    public static string CreateToken(
        DemoAccount account,
        AuthOptions options)
    {
        var settings = options.DevelopmentIssuer;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSigningKey(settings)));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id),
            new("name", account.Name),
            new("email", account.Email),
            new(options.RoleClaim, account.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Only a member borrows as themselves; staff act on behalf of whoever is at
        // the desk, so their tokens carry no member id.
        if (account.MemberId is { } memberId)
            claims.Add(new Claim(CurrentUser.MemberIdClaim, memberId.ToString()));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(settings.TokenLifetimeHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Returns the configured signing key, or a deterministic local one.
    /// </summary>
    /// <remarks>
    /// A generated-per-start key would invalidate every token on restart, which makes
    /// a demo maddening. A fixed local default is fine precisely because this issuer
    /// is disabled the moment a real provider is configured.
    /// </remarks>
    public static string ResolveSigningKey(DevelopmentIssuerOptions settings) =>
        string.IsNullOrWhiteSpace(settings.SigningKey)
            ? "athenaeum-local-development-signing-key-not-for-deployment"
            : settings.SigningKey;
}
