namespace Library.Domain.Entities;

/// <summary>
/// Lifecycle of a physical item. Persisted as <c>int</c>: the values are part of the
/// database contract and must not be reordered.
/// </summary>
public enum CopyStatus
{
    /// <summary>On the shelf and lendable.</summary>
    Available = 0,

    /// <summary>Currently held by a member.</summary>
    OnLoan = 1,

    /// <summary>Reported missing. Kept for history rather than deleted.</summary>
    Lost = 2,

    /// <summary>Retired from circulation (damaged, superseded edition).</summary>
    Withdrawn = 3
}
