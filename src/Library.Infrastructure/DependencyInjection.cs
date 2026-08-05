using Library.Application.Abstractions;
using Library.Application.Catalog;
using Library.Application.Common;
using Library.Application.Loans;
using Library.Infrastructure.Persistence;
using Library.Infrastructure.Persistence.Queries;
using Library.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

/// <summary>
/// Composition root for everything that touches the outside world. The API project
/// calls this and stays unaware of EF Core, SQL Server or any HTTP client.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LibraryDb")
            ?? throw new InvalidOperationException(
                "No connection string named 'LibraryDb' was found. See README.md for local setup.");

        services.AddDbContext<LibraryDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // The database is a network dependency like any other. Transient faults
                // - a failover, a dropped connection, a throttled Azure SQL tier - are
                // expected rather than exceptional, so they are retried rather than
                // surfaced to the user.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
                sql.CommandTimeout(30);
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LibraryDbContext>());
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IBarcodeGenerator, SequentialBarcodeGenerator>();

        services.AddSingleton<IClock, SystemClock>();

        // Use cases. They depend only on the ports above, which is what keeps them
        // testable without a database.
        services.AddScoped<CatalogService>();
        services.AddScoped<LoanService>();

        return services;
    }
}
