using System.Security.Claims;
using System.Text;
using Library.Application.Abstractions;
using Library.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Library.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddLibraryAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
                      ?? new AuthOptions();

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        if (!options.UsesExternalProvider && !options.DevelopmentIssuer.Enabled)
        {
            throw new InvalidOperationException(
                "Authentication is not configured. Set Authentication:Authority to an OIDC provider, " +
                "or enable Authentication:DevelopmentIssuer for local use. See README.md.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;   // keep claim names as the provider sent them

                if (options.UsesExternalProvider)
                {
                    // Signing keys come from the provider's discovery document and are
                    // refreshed on rotation. Nothing about the key material is stored here.
                    jwt.Authority = options.Authority;
                    jwt.Audience = options.Audience;
                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        NameClaimType = "name",
                        RoleClaimType = ClaimTypes.Role,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                }
                else
                {
                    var key = DevelopmentTokenIssuer.ResolveSigningKey(options.DevelopmentIssuer);

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = options.DevelopmentIssuer.Issuer,
                        ValidateAudience = true,
                        ValidAudience = options.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        NameClaimType = "name",
                        RoleClaimType = ClaimTypes.Role,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                }

                // Providers disagree about where roles live, so they are copied into the
                // standard role claim once, here, rather than every policy having to know.
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                            return Task.CompletedTask;

                        // Materialised before the loop: FindAll is lazy over the same
                        // claim collection that AddClaim below mutates.
                        var incoming = identity.FindAll(options.RoleClaim)
                            .Select(c => c.Value)
                            .SelectMany(SplitRoles)
                            .Where(LibraryRole.IsKnown)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        foreach (var role in incoming)
                        {
                            if (!identity.HasClaim(ClaimTypes.Role, role))
                                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            // Anything that reads the catalogue: everyone signed in, whatever their role.
            .AddPolicy(Policies.ReadCatalog, p => p.RequireAuthenticatedUser())

            // Cataloguing is desk work.
            .AddPolicy(Policies.WriteCatalog, p =>
                p.RequireRole(LibraryRole.Librarian, LibraryRole.Admin))

            // Lending and returns are desk work too.
            .AddPolicy(Policies.ManageLoans, p =>
                p.RequireRole(LibraryRole.Librarian, LibraryRole.Admin))

            .AddPolicy(Policies.ViewAllLoans, p =>
                p.RequireRole(LibraryRole.Librarian, LibraryRole.Admin))

            // Removing a title destroys its loan history with it. Administrators only.
            .AddPolicy(Policies.DeleteCatalog, p =>
                p.RequireRole(LibraryRole.Admin));

        return services;
    }

    /// <summary>
    /// Accepts roles as repeated claims, a JSON array, or a space/comma separated
    /// string - all three are in the wild.
    /// </summary>
    private static IEnumerable<string> SplitRoles(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1].Replace("\"", string.Empty);
        }

        return trimmed
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
