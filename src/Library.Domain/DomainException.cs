namespace Library.Domain;

/// <summary>
/// Raised when an operation would break a library rule — lending a copy that is
/// already out, deleting a title someone still holds, and so on.
/// </summary>
/// <remarks>
/// Distinct from an infrastructure failure on purpose: the API layer translates this
/// into a 4xx <c>ProblemDetails</c> because the caller can fix it, whereas an
/// unexpected exception is a 500 because they cannot.
/// </remarks>
public class DomainException(string message) : Exception(message);
