using Library.Application.Common;

namespace Library.Api;

/// <summary>
/// Turns a use-case <see cref="Result{T}"/> into an HTTP response.
/// </summary>
/// <remarks>
/// This is the only place that knows which status code an <see cref="ErrorKind"/>
/// deserves. Keeping the mapping here means the application layer never mentions
/// HTTP, and a new use case gets consistent error responses for free.
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.Match(
            onSuccess: TypedResults.Ok,
            onError: Problem);

    /// <summary>For creations, so a successful POST answers 201 with a Location header.</summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> location) =>
        result.Match(
            onSuccess: value => Results.Created(location(value), value),
            onError: Problem);

    public static IResult ToNoContentResult<T>(this Result<T> result) =>
        result.Match(
            onSuccess: _ => Results.NoContent(),
            onError: Problem);

    private static IResult Problem(ErrorKind kind, string error)
    {
        var (status, title) = kind switch
        {
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorKind.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        return Results.Problem(detail: error, statusCode: status, title: title);
    }
}
