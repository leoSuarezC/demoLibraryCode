namespace Library.Application.Abstractions;

/// <summary>
/// Commits everything the repositories have staged as one transaction.
/// </summary>
/// <remarks>
/// Repositories deliberately do not save on their own. A use case that registers a
/// title and its three copies should produce one commit, not four, and should leave
/// nothing behind if the third copy is rejected.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
