using Library.Application.Abstractions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public class MemberRepository(LibraryDbContext db) : IMemberRepository
{
    public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Member>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Members
            .Where(m => m.IsActive)
            .OrderBy(m => m.FullName)
            .ToListAsync(ct);

    public Task<int> CountOpenLoansAsync(Guid memberId, CancellationToken ct = default) =>
        db.Loans.CountAsync(l => l.MemberId == memberId && l.ReturnedAtUtc == null, ct);
}
