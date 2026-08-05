using Library.Application.Abstractions;
using Library.Domain;

namespace Library.Api.Endpoints;

public record EnrichMetadataRequest(string Title, string Author);

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        // Enrichment spends a rate-limited quota on an external service. It is
        // restricted to the people who catalogue, which is also the only place the
        // result is useful.
        var ai = app.MapGroup("/api/ai")
            .WithTags("AI")
            .RequireAuthorization(Policies.WriteCatalog);

        // The UI asks first and hides the button when enrichment is unavailable,
        // rather than offering an action that is guaranteed to disappoint.
        ai.MapGet("/status", (IAiMetadataService service) =>
                Results.Ok(new { enabled = service.IsConfigured }))
            .WithName("GetAiStatus")
            .WithSummary("Report whether metadata enrichment is configured");

        ai.MapPost("/enrich-metadata", async (
                EnrichMetadataRequest request,
                IAiMetadataService service,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Author))
                    return Results.Problem(
                        detail: "Both a title and an author are needed to suggest metadata.",
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid request");

                if (!service.IsConfigured)
                    return Results.Problem(
                        detail: "Metadata enrichment is not configured on this deployment.",
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service unavailable");

                var suggestion = await service.SuggestAsync(request.Title, request.Author, ct);

                // A model that fails or declines is not an error the librarian caused,
                // and cataloguing must continue without it.
                return suggestion is null
                    ? Results.Problem(
                        detail: "No suggestion could be produced. Please fill the fields in manually.",
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "No suggestion available")
                    : Results.Ok(suggestion);
            })
            .WithName("EnrichMetadata")
            .WithSummary("Draft synopsis, category, year and tags for a title")
            .WithDescription(
                "Returns a suggestion for a librarian to review. Nothing is written to the " +
                "catalogue: the response populates the form and a human confirms it.");

        return app;
    }
}
