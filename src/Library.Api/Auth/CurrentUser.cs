using System.Security.Claims;
using Library.Application.Abstractions;

namespace Library.Api.Auth;

/// <summary>
/// Reads the caller's identity off the validated token.
/// </summary>
/// <remarks>
/// The only place that touches <see cref="ClaimsPrincipal"/>. Everything inward of
/// here sees <see cref="ICurrentUser"/>.
/// </remarks>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Links a token to the member record it borrows as.</summary>
    public const string MemberIdClaim = "member_id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? SubjectId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? Name =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("name");

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray() ?? [];

    public Guid? MemberId =>
        Guid.TryParse(Principal?.FindFirstValue(MemberIdClaim), out var id) ? id : null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
