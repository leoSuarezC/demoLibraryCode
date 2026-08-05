using Library.Api.Auth;
using Library.Application.Abstractions;
using Library.Application.Members;
using Library.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Library.Api.Endpoints;

public record TokenRequest(string AccountId);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        // Lets the frontend render the right sign-in experience without guessing:
        // a provider redirect, or the local account picker.
        auth.MapGet("/config", (IOptions<AuthOptions> options) =>
            {
                var o = options.Value;
                return Results.Ok(new
                {
                    mode = o.UsesExternalProvider ? "oidc" : "development",
                    authority = o.UsesExternalProvider ? o.Authority : null,
                    audience = o.Audience,
                    roles = LibraryRole.All
                });
            })
            .AllowAnonymous()
            .WithName("GetAuthConfig")
            .WithSummary("Report how this deployment authenticates callers");

        auth.MapGet("/me", (ICurrentUser user) =>
                user.IsAuthenticated
                    ? Results.Ok(new
                    {
                        subjectId = user.SubjectId,
                        name = user.Name,
                        email = user.Email,
                        roles = user.Roles,
                        memberId = user.MemberId
                    })
                    : Results.Unauthorized())
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Describe the caller behind the current token");

        MapDevelopmentSignIn(auth);

        return app;
    }

    /// <summary>
    /// Local sign-in, available only when no external provider is configured.
    /// </summary>
    /// <remarks>
    /// Both routes check <see cref="AuthOptions.UsesExternalProvider"/> at request
    /// time and answer 404 when a real provider is in play, so a deployment that is
    /// wired to Auth0 does not also quietly accept hand-issued tokens.
    /// </remarks>
    private static void MapDevelopmentSignIn(RouteGroupBuilder auth)
    {
        auth.MapGet("/accounts", async (
                IOptions<AuthOptions> options,
                MemberService members,
                CancellationToken ct) =>
            {
                if (Disabled(options.Value)) return Results.NotFound();

                var accounts = new List<DemoAccount>(DevelopmentTokenIssuer.StaffAccounts);

                // Every seeded borrower is offered as a sign-in, so the member-side
                // restrictions can be seen from the borrower's own point of view.
                foreach (var member in await members.GetActiveAsync(ct))
                {
                    accounts.Add(new DemoAccount(
                        Id: $"member-{member.Id}",
                        Name: member.FullName,
                        Email: member.Email,
                        Role: LibraryRole.Member,
                        MemberId: member.Id,
                        Description: member.OpenLoans == 1
                            ? "Borrower · 1 item out"
                            : $"Borrower · {member.OpenLoans} items out"));
                }

                return Results.Ok(accounts);
            })
            .AllowAnonymous()
            .WithName("GetDemoAccounts")
            .WithSummary("List the accounts the local sign-in will issue tokens for");

        auth.MapPost("/token", async (
                TokenRequest request,
                IOptions<AuthOptions> options,
                MemberService members,
                CancellationToken ct) =>
            {
                if (Disabled(options.Value)) return Results.NotFound();

                var account = await ResolveAccountAsync(request.AccountId, members, ct);
                if (account is null)
                    return Results.Problem(
                        detail: "No demo account with that id.",
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not found");

                return Results.Ok(new
                {
                    accessToken = DevelopmentTokenIssuer.CreateToken(account, options.Value),
                    tokenType = "Bearer",
                    expiresInSeconds = options.Value.DevelopmentIssuer.TokenLifetimeHours * 3600,
                    account
                });
            })
            .AllowAnonymous()
            .WithName("IssueDemoToken")
            .WithSummary("Issue a token for one of the demo accounts")
            .WithDescription(
                "Local convenience so the system can be reviewed without registering with an " +
                "identity provider. Refused with 404 whenever Authentication:Authority is set.");
    }

    private static bool Disabled(AuthOptions options) =>
        options.UsesExternalProvider || !options.DevelopmentIssuer.Enabled;

    private static async Task<DemoAccount?> ResolveAccountAsync(
        string accountId,
        MemberService members,
        CancellationToken ct)
    {
        var staff = DevelopmentTokenIssuer.StaffAccounts
            .FirstOrDefault(a => a.Id == accountId);
        if (staff is not null) return staff;

        if (!accountId.StartsWith("member-", StringComparison.Ordinal)) return null;
        if (!Guid.TryParse(accountId["member-".Length..], out var memberId)) return null;

        var member = (await members.GetActiveAsync(ct)).FirstOrDefault(m => m.Id == memberId);
        if (member is null) return null;

        return new DemoAccount(
            accountId,
            member.FullName,
            member.Email,
            LibraryRole.Member,
            member.Id,
            "Borrower");
    }
}
