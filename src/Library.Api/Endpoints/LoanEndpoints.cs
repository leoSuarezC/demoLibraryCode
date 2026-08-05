using Library.Application.Abstractions;
using Library.Application.Contracts;
using Library.Application.Loans;
using Library.Domain;

namespace Library.Api.Endpoints;

public static class LoanEndpoints
{
    /// <summary>
    /// Header carrying the caller's idempotency key. Named after the convention used
    /// by Stripe and others, so it needs no explaining to anyone integrating.
    /// </summary>
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var loans = app.MapGroup("/api/loans")
            .WithTags("Loans")
            .RequireAuthorization(Policies.ReadCatalog);

        loans.MapPost("/checkout", async (
                CheckoutRequest request,
                HttpContext http,
                LoanService service,
                CancellationToken ct) =>
            {
                // The key comes from the caller, not from the server: the point is that
                // a client which never saw our response can retry with the same key and
                // still get one loan.
                var key = http.Request.Headers[IdempotencyKeyHeader].ToString();

                var result = await service.CheckoutAsync(request, key, ct);

                if (result is { IsSuccess: true, Value.WasAlreadyProcessed: true })
                {
                    // Distinguishes a replay from a fresh loan without failing the
                    // request - the caller's intent was satisfied either way.
                    http.Response.Headers["Idempotent-Replay"] = "true";
                }

                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.ManageLoans)
            .WithName("CheckoutBook")
            .WithSummary("Borrow an available copy of a title")
            .WithDescription(
                "Desk operation: staff lend to a member, so the borrower is named in the body " +
                "rather than taken from the token. Requires an Idempotency-Key header; repeating " +
                "a request with the same key returns the original loan and sets " +
                "Idempotent-Replay: true rather than lending a second copy.");

        loans.MapPost("/checkin", async (
                CheckinRequest request,
                LoanService service,
                CancellationToken ct) =>
                (await service.CheckinAsync(request, ct)).ToHttpResult())
            .RequireAuthorization(Policies.ManageLoans)
            .WithName("CheckinBook")
            .WithSummary("Return a copy and settle any overdue fee");

        loans.MapGet("/open", async (int? limit, LoanService service, CancellationToken ct) =>
                Results.Ok(await service.GetOpenLoansAsync(Math.Clamp(limit ?? 100, 1, 500), ct)))
            .RequireAuthorization(Policies.ViewAllLoans)
            .WithName("GetOpenLoans")
            .WithSummary("List loans still outstanding across the library, most overdue first");

        loans.MapGet("/mine", async (
                ICurrentUser user,
                LoanService service,
                CancellationToken ct) =>
            {
                // The member id is read from the validated token, never from the request.
                // A borrower cannot ask for somebody else's loans because there is no
                // parameter through which to ask.
                if (user.MemberId is not { } memberId)
                    return Results.Ok(Array.Empty<LoanSummary>());

                return Results.Ok(await service.GetLoansForMemberAsync(memberId, ct));
            })
            .WithName("GetMyLoans")
            .WithSummary("List what the signed-in member is currently holding")
            .WithDescription(
                "Staff accounts have no member record and receive an empty list; they use " +
                "/api/loans/open instead.");

        return app;
    }
}
