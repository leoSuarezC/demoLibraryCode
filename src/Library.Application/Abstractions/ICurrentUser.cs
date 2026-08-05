namespace Library.Application.Abstractions;

/// <summary>
/// Who is making the current request.
/// </summary>
/// <remarks>
/// A port, so use cases can ask "who is this?" without referencing HttpContext or
/// ClaimsPrincipal - and so tests can answer the question directly instead of
/// building an identity.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable identifier from the identity provider (the <c>sub</c> claim).</summary>
    string? SubjectId { get; }

    string? Name { get; }

    string? Email { get; }

    /// <summary>Role names carried by the token.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// The library member this user borrows as, when the token carries one.
    /// </summary>
    /// <remarks>
    /// Staff accounts have no member record: a librarian operating the desk acts on
    /// behalf of whoever is standing in front of them, not as themselves.
    /// </remarks>
    Guid? MemberId { get; }

    bool IsInRole(string role);
}
