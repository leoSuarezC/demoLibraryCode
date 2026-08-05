namespace Library.Domain;

/// <summary>
/// Who someone is at the library, and therefore what they may do.
/// </summary>
/// <remarks>
/// Three roles rather than a permission matrix, because a small library has three
/// kinds of person and inventing finer grain would be modelling an organisation that
/// does not exist. The named policies in the API layer are the seam: if the roles
/// ever need splitting, the endpoints do not change.
/// </remarks>
public static class LibraryRole
{
    /// <summary>Borrows and searches. Cannot alter the catalogue.</summary>
    public const string Member = "Member";

    /// <summary>Works the desk: lends, takes returns, catalogues.</summary>
    public const string Librarian = "Librarian";

    /// <summary>Everything a librarian can do, plus removing records outright.</summary>
    public const string Admin = "Admin";

    public static readonly string[] All = [Member, Librarian, Admin];

    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Named authorisation policies. Endpoints reference these rather than role names, so
/// "who may lend a book" is answered in one place instead of at every call site.
/// </summary>
public static class Policies
{
    /// <summary>Browse and search the catalogue. Any signed-in user.</summary>
    public const string ReadCatalog = "catalog:read";

    /// <summary>Add or correct titles and copies. Desk staff.</summary>
    public const string WriteCatalog = "catalog:write";

    /// <summary>Remove a title outright. Administrators only - it is not reversible.</summary>
    public const string DeleteCatalog = "catalog:delete";

    /// <summary>Lend and take returns. Desk staff.</summary>
    public const string ManageLoans = "loans:manage";

    /// <summary>See the whole library's outstanding loans, not just one's own.</summary>
    public const string ViewAllLoans = "loans:view-all";
}
