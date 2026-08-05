using Library.Application.Members;

namespace Library.Api.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/members", async (MemberService members, CancellationToken ct) =>
                Results.Ok(await members.GetActiveAsync(ct)))
            .WithTags("Members")
            .WithName("GetMembers")
            .WithSummary("List active members with how many items each is holding");

        return app;
    }
}
