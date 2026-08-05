using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Brings the database to a usable state on start-up: schema, then seed data.
/// </summary>
/// <remarks>
/// Running migrations from the application is a deliberate trade for a single-service
/// deployment and an assignment a reviewer should be able to run with one command. A
/// system with several instances would move this to a release step so that N starting
/// replicas do not race to migrate the same database.
/// </remarks>
public static class DatabaseInitializer
{
    public static async Task InitialiseAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseInitializer));

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);

        // Procedures come after migrations: they reference tables the migrations create.
        logger.LogInformation("Applying stored procedures...");
        await SqlScriptRunner.ApplyStoredProceduresAsync(db, logger, ct);

        logger.LogInformation("Seeding catalogue if empty...");
        await DatabaseSeeder.SeedAsync(db, ct);

        logger.LogInformation("Database ready.");
    }
}
