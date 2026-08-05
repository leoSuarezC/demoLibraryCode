using Library.Application.Catalog;
using Library.Application.Contracts;

namespace Library.Api.Endpoints;

public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/books")
            .WithTags("Books");

        books.MapGet("/", async (
                string? query,
                string? category,
                bool? availableOnly,
                int? page,
                int? pageSize,
                CatalogService catalog,
                CancellationToken ct) =>
            {
                var criteria = new SearchBooksQuery(
                    query,
                    category,
                    availableOnly ?? false,
                    page ?? 1,
                    pageSize ?? 20).Normalised();

                return Results.Ok(await catalog.SearchAsync(criteria, ct));
            })
            .WithName("SearchBooks")
            .WithSummary("Search the catalogue")
            .WithDescription(
                "Free-text search across title, author, ISBN, category and tags, ranked by " +
                "how directly the term matches. Results are paged; paging arguments are clamped.");

        books.MapGet("/categories", async (CatalogService catalog, CancellationToken ct) =>
                Results.Ok(await catalog.GetCategoriesAsync(ct)))
            .WithName("GetCategories")
            .WithSummary("List the categories present in the catalogue");

        books.MapGet("/{id:guid}", async (Guid id, CatalogService catalog, CancellationToken ct) =>
                (await catalog.GetAsync(id, ct)).ToHttpResult())
            .WithName("GetBook")
            .WithSummary("Get one title with its copies and their current loans");

        books.MapPost("/", async (
                CreateBookRequest request,
                CatalogService catalog,
                CancellationToken ct) =>
            {
                var result = await catalog.CreateAsync(request, ct);
                return result.ToCreatedResult(book => $"/api/books/{book.Id}");
            })
            .WithName("CreateBook")
            .WithSummary("Register a new title and its first copies");

        books.MapPut("/{id:guid}", async (
                Guid id,
                UpdateBookRequest request,
                CatalogService catalog,
                CancellationToken ct) =>
                (await catalog.UpdateAsync(id, request, ct)).ToHttpResult())
            .WithName("UpdateBook")
            .WithSummary("Correct a title's details");

        books.MapPost("/{id:guid}/copies", async (
                Guid id,
                AddCopyRequest request,
                CatalogService catalog,
                CancellationToken ct) =>
                (await catalog.AddCopyAsync(id, request, ct)).ToHttpResult())
            .WithName("AddCopy")
            .WithSummary("Register another physical copy of a title")
            .WithDescription("A barcode may be supplied; if omitted, the next one in sequence is issued.");

        books.MapDelete("/{id:guid}", async (Guid id, CatalogService catalog, CancellationToken ct) =>
                (await catalog.DeleteAsync(id, ct)).ToNoContentResult())
            .WithName("DeleteBook")
            .WithSummary("Remove a title")
            .WithDescription("Refused with 409 while any copy is still on loan.");

        return app;
    }
}
