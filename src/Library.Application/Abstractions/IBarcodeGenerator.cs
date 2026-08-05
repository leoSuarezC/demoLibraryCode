namespace Library.Application.Abstractions;

/// <summary>
/// Produces the label a new physical item will carry.
/// </summary>
/// <remarks>
/// A port rather than a helper because a real library's barcodes are dictated by the
/// label printer and the scheme already on its shelves. Swapping the implementation
/// should not touch the use cases.
/// </remarks>
public interface IBarcodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
