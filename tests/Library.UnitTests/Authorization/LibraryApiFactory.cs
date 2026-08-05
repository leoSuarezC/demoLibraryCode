using Library.Application.Abstractions;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Domain.Entities;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Library.UnitTests.Authorization;

/// <summary>
/// Hosts the real API in-process for authorisation tests.
/// </summary>
/// <remarks>
/// What is under test is the pipeline: routing, token validation, policy evaluation
/// and the status code that falls out. All of that runs before a handler touches the
/// database, so persistence is substituted rather than provisioned.
///
/// Deliberately the real <c>Program</c>. The policies, group defaults and per-endpoint
/// requirements are exactly the ones that ship; a host assembled by hand for tests
/// could pass while production was wide open.
/// </remarks>
public class LibraryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:InitialiseOnStartup", "false");
        builder.UseSetting("Authentication:Authority", "");
        builder.UseSetting("Authentication:DevelopmentIssuer:Enabled", "true");
        builder.UseSetting("Authentication:DevelopmentIssuer:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("Authentication:DevelopmentIssuer:Issuer", TestTokens.Issuer);
        builder.UseSetting("Authentication:Audience", TestTokens.Audience);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LibraryDbContext>>();
            services.RemoveAll<LibraryDbContext>();
            services.AddDbContext<LibraryDbContext>(o =>
                o.UseInMemoryDatabase($"authz-{Guid.NewGuid()}"));

            // Check-out runs inside a stored procedure, which no in-memory provider can
            // execute. Substituted so an *authorised* request still returns something
            // coherent, instead of failing for an unrelated reason and muddying the
            // assertion about who was let through.
            services.RemoveAll<ILoanRepository>();
            var loans = Substitute.For<ILoanRepository>();
            loans.CheckoutAsync(default, default, default!, default, default)
                .ReturnsForAnyArgs(new CheckoutCommandResult(CheckoutOutcome.BookNotFound, null));
            loans.GetOpenLoanForCopyAsync(default).ReturnsForAnyArgs((Loan?)null);
            loans.GetOpenLoansAsync().ReturnsForAnyArgs(Array.Empty<Loan>());
            loans.GetOpenLoansForMemberAsync(default).ReturnsForAnyArgs(Array.Empty<Loan>());
            services.AddScoped(_ => loans);

            services.RemoveAll<ICatalogQueries>();
            var queries = Substitute.For<ICatalogQueries>();
            queries.SearchAsync(default!).ReturnsForAnyArgs(PagedResult<BookSummary>.Empty(1, 20));
            queries.GetCategoriesAsync().ReturnsForAnyArgs(Array.Empty<string>());
            queries.GetDetailAsync(default).ReturnsForAnyArgs((BookDetail?)null);
            services.AddScoped(_ => queries);
        });
    }
}
