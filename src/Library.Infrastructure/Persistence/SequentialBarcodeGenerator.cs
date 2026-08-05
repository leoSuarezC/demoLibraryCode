using Library.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Issues barcodes in the form <c>LIB-00001</c>, continuing from the highest label
/// already on the shelves.
/// </summary>
/// <remarks>
/// Deliberately simple, and honest about it: reading the maximum and adding one is a
/// read-modify-write with the same race as any other. The unique index on Barcode
/// turns a collision into a failed insert rather than two items sharing a label, and
/// a real deployment would take these from the label printer's own sequence anyway.
/// </remarks>
public class SequentialBarcodeGenerator(LibraryDbContext db) : IBarcodeGenerator
{
    private const string Prefix = "LIB-";

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var lastNumber = await db.BookCopies
            .Where(c => c.Barcode.StartsWith(Prefix))
            .Select(c => c.Barcode)
            .OrderByDescending(b => b)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (lastNumber is not null
            && int.TryParse(lastNumber[Prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{Prefix}{next:D5}";
    }
}
