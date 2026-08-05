namespace Library.Application.Common;

/// <summary>
/// Why a use case refused to do what was asked. The API layer maps these to status
/// codes, which keeps HTTP concerns out of the application layer.
/// </summary>
public enum ErrorKind
{
    None = 0,

    /// <summary>The thing being addressed does not exist. → 404</summary>
    NotFound,

    /// <summary>The request is malformed or breaks a rule. → 400</summary>
    Validation,

    /// <summary>The request is valid but conflicts with current state. → 409</summary>
    Conflict,

    /// <summary>A dependency is unavailable and the caller may retry. → 503</summary>
    Unavailable
}

/// <summary>
/// Outcome of a use case: either a value or a reason it could not be produced.
/// </summary>
/// <remarks>
/// Expected refusals — an unknown book, a copy already lent — are ordinary control
/// flow in a library, not exceptional conditions. Modelling them as return values
/// keeps them visible in the signature and reserves exceptions for genuine faults.
/// </remarks>
public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Error = null;
        Kind = ErrorKind.None;
    }

    private Result(ErrorKind kind, string error)
    {
        _value = default;
        Error = error;
        Kind = kind;
    }

    public bool IsSuccess => Kind == ErrorKind.None;
    public ErrorKind Kind { get; }
    public string? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result carries no value: {Error}");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> NotFound(string error) => new(ErrorKind.NotFound, error);
    public static Result<T> Invalid(string error) => new(ErrorKind.Validation, error);
    public static Result<T> Conflict(string error) => new(ErrorKind.Conflict, error);
    public static Result<T> Unavailable(string error) => new(ErrorKind.Unavailable, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<ErrorKind, string, TOut> onError) =>
        IsSuccess ? onSuccess(_value!) : onError(Kind, Error!);
}
