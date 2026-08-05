using Library.Application.Abstractions;

namespace Library.Application.Members;

public record MemberSummary(
    Guid Id,
    string FullName,
    string Email,
    string MembershipCode,
    int OpenLoans);

/// <summary>
/// Members, as far as the lending desk needs them: who can borrow, and how much they
/// are already holding.
/// </summary>
public class MemberService(IMemberRepository members)
{
    public async Task<IReadOnlyList<MemberSummary>> GetActiveAsync(CancellationToken ct = default)
    {
        var active = await members.GetActiveAsync(ct);

        var summaries = new List<MemberSummary>(active.Count);
        foreach (var member in active)
        {
            summaries.Add(new MemberSummary(
                member.Id,
                member.FullName,
                member.Email,
                member.MembershipCode,
                await members.CountOpenLoansAsync(member.Id, ct)));
        }

        return summaries;
    }
}
