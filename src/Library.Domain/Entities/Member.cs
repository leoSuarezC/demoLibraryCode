namespace Library.Domain.Entities;

/// <summary>Someone entitled to borrow from the library.</summary>
public class Member
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    /// <summary>Human-readable identifier printed on the membership card, e.g. <c>MEM-0042</c>.</summary>
    public string MembershipCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;
    public DateTime JoinedAtUtc { get; private set; } = DateTime.UtcNow;

    private Member() { }

    public Member(string fullName, string email, string membershipCode)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("A member must have a name.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException($"'{email}' is not a usable email address.");
        if (string.IsNullOrWhiteSpace(membershipCode))
            throw new DomainException("A member must have a membership code.");

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        MembershipCode = membershipCode.Trim().ToUpperInvariant();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
