namespace Library.Application.Common;

/// <summary>
/// The current time, injected rather than read from <see cref="DateTime"/> directly.
/// </summary>
/// <remarks>
/// Overdue detection and fee calculation are entirely about dates. Without a seam
/// here, testing "this loan is four days late" means either waiting four days or
/// back-dating rows.
/// </remarks>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
