using Library.Domain.Entities;

namespace Library.Application.Abstractions;

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Member>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Open loans currently held by a member, used to enforce the borrowing limit.</summary>
    Task<int> CountOpenLoansAsync(Guid memberId, CancellationToken ct = default);
}
