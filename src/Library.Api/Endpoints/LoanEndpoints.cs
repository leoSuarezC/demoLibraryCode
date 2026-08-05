using Library.Application.Contracts;
using Library.Application.Loans;

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
            .WithTags("Loans");

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
            .WithName("CheckoutBook")
            .WithSummary("Borrow an available copy of a title")
            .WithDescription(
                "Requires an Idempotency-Key header. Repeating a request with the same key " +
                "returns the original loan and sets Idempotent-Replay: true rather than " +
                "lending a second copy. The library picks which copy leaves the shelf.");

        loans.MapPost("/checkin", async (
                CheckinRequest request,
                LoanService service,
                CancellationToken ct) =>
                (await service.CheckinAsync(request, ct)).ToHttpResult())
            .WithName("CheckinBook")
            .WithSummary("Return a copy and settle any overdue fee");

        loans.MapGet("/open", async (int? limit, LoanService service, CancellationToken ct) =>
                Results.Ok(await service.GetOpenLoansAsync(Math.Clamp(limit ?? 100, 1, 500), ct)))
            .WithName("GetOpenLoans")
            .WithSummary("List loans still outstanding, most overdue first");

        return app;
    }
}
