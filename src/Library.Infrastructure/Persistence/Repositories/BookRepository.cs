using Library.Application.Abstractions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public class BookRepository(LibraryDbContext db) : IBookRepository
{
    public Task<Book?> GetByIdAsync(Guid id, bool includeCopies = true, CancellationToken ct = default)
    {
        var query = db.Books.AsQueryable();
        if (includeCopies) query = query.Include(b => b.Copies);
        return query.FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public Task<bool> IsbnExistsAsync(string isbn, Guid? excludingBookId = null, CancellationToken ct = default) =>
        db.Books.AnyAsync(b => b.Isbn == isbn && (excludingBookId == null || b.Id != excludingBookId), ct);

    public Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct = default) =>
        db.BookCopies.AnyAsync(c => c.Barcode == barcode, ct);

    public void Add(Book book) => db.Books.Add(book);

    public void Remove(Book book) => db.Books.Remove(book);
}
