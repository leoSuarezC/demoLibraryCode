using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Library.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Library.UnitTests.Authorization;

/// <summary>
/// Mints the bearer tokens the tests present.
/// </summary>
/// <remarks>
/// Real signed JWTs, validated by the API's real validation parameters, rather than a
/// stub authentication handler. A handler that simply asserts an identity would skip
/// exactly the part most likely to be misconfigured - signature, issuer, audience,
/// expiry and where the role claim is read from.
/// </remarks>
public static class TestTokens
{
    public const string SigningKey = "unit-test-signing-key-long-enough-for-hmac-sha256";
    public const string Issuer = "https://localhost/athenaeum-test";
    public const string Audience = "library-api";

    public static string For(string role, Guid? memberId = null) =>
        Build(claims =>
        {
            claims.Add(new Claim("roles", role));
            if (memberId is { } id)
                claims.Add(new Claim("member_id", id.ToString()));
        });

    public static string Admin() => For(LibraryRole.Admin);

    public static string Librarian() => For(LibraryRole.Librarian);

    public static string Member(Guid? memberId = null) =>
        For(LibraryRole.Member, memberId ?? Guid.NewGuid());

    /// <summary>A validly signed token carrying no role at all.</summary>
    public static string WithoutRoles() => Build(_ => { });

    /// <summary>Correctly signed but expired, to prove lifetime is actually checked.</summary>
    public static string Expired() =>
        Build(
            claims => claims.Add(new Claim("roles", LibraryRole.Admin)),
            notBefore: DateTime.UtcNow.AddHours(-4),
            expires: DateTime.UtcNow.AddHours(-2));

    /// <summary>Well-formed and correctly shaped, but signed with the wrong key.</summary>
    public static string SignedWithWrongKey() =>
        Build(
            claims => claims.Add(new Claim("roles", LibraryRole.Admin)),
            signingKey: "a-completely-different-key-also-long-enough-for-hs256");

    private static string Build(
        Action<List<Claim>> addClaims,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string? signingKey = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("name", "Test User"),
            new("email", "test.user@athenaeum.test")
        };
        addClaims(claims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
