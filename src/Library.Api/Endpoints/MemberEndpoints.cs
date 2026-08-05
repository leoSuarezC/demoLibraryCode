using Library.Application.Members;
using Library.Domain;

namespace Library.Api.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        // The roster carries names and email addresses of everyone who borrows here.
        // Desk staff need it to lend; one borrower has no business reading it.
        app.MapGet("/api/members", async (MemberService members, CancellationToken ct) =>
                Results.Ok(await members.GetActiveAsync(ct)))
            .RequireAuthorization(Policies.ManageLoans)
            .WithTags("Members")
            .WithName("GetMembers")
            .WithSummary("List active members with how many items each is holding");

        return app;
    }
}
