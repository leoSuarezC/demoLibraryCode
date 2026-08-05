using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded stored procedures to the database.
/// </summary>
/// <remarks>
/// Every script is written as CREATE OR ALTER, so running this on each start-up is
/// safe and always leaves the database holding the version that shipped with the
/// running build. Procedures are treated as code rather than as migration steps
/// precisely so they can be reviewed as diffs instead of as a chain of drop/recreate
/// migrations.
/// </remarks>
public static class SqlScriptRunner
{
    private const string ResourcePrefix = "Library.Infrastructure.Persistence.Sql.";

    public static async Task ApplyStoredProceduresAsync(
        LibraryDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var assembly = typeof(SqlScriptRunner).Assembly;

        var scripts = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                        && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (scripts.Length == 0)
        {
            logger.LogWarning("No SQL scripts were found embedded in {Assembly}.", assembly.GetName().Name);
            return;
        }

        foreach (var resourceName in scripts)
        {
            var sql = await ReadResourceAsync(assembly, resourceName, ct);

            // ExecuteSqlRawAsync sends one batch, which is exactly what each script is:
            // CREATE OR ALTER PROCEDURE must be the first statement in its batch, so the
            // scripts deliberately contain no GO separators.
            await db.Database.ExecuteSqlRawAsync(sql, ct);

            logger.LogInformation("Applied {Script}.", resourceName[ResourcePrefix.Length..]);
        }
    }

    private static async Task<string> ReadResourceAsync(
        Assembly assembly,
        string resourceName,
        CancellationToken ct)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{resourceName}' could not be opened.");

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
