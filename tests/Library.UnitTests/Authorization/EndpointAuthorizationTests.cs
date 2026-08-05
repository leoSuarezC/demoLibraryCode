using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Library.Domain;

namespace Library.UnitTests.Authorization;

/// <summary>
/// The access-control matrix, as executable statements rather than prose in a README.
/// </summary>
/// <remarks>
/// Every case asserts a status code the middleware produces before any handler runs,
/// so these hold regardless of what the database contains:
///
///   401  no usable token
///   403  a valid token whose role is not enough
///   not 401/403  the caller was let through (what happens next is another test's job)
/// </remarks>
public class EndpointAuthorizationTests(LibraryApiFactory factory)
    : IClassFixture<LibraryApiFactory>
{
    private HttpClient Client(string? token = null)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ------------------------------------------------------------- open to all

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/auth/config")]
    [InlineData("/api/auth/accounts")]
    public async Task Public_endpoints_need_no_token(string path)
    {
        var response = await Client().GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------- nothing else is public

    [Theory]
    [InlineData("GET", "/api/books")]
    [InlineData("GET", "/api/books/categories")]
    [InlineData("GET", "/api/loans/open")]
    [InlineData("GET", "/api/loans/mine")]
    [InlineData("GET", "/api/members")]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/loans/checkout")]
    [InlineData("POST", "/api/loans/checkin")]
    [InlineData("POST", "/api/books")]
    [InlineData("POST", "/api/ai/enrich-metadata")]
    public async Task Every_other_endpoint_refuses_an_anonymous_caller(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { });

        var response = await Client().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------- token must be sound

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        var response = await Client(TestTokens.Expired()).GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_is_refused()
    {
        var response = await Client(TestTokens.SignedWithWrongKey()).GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Garbage_in_the_authorization_header_is_refused()
    {
        var response = await Client("not-a-jwt").GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------ reading

    [Fact]
    public async Task Any_signed_in_role_may_search_the_catalogue()
    {
        foreach (var token in new[] { TestTokens.Member(), TestTokens.Librarian(), TestTokens.Admin() })
        {
            var response = await Client(token).GetAsync("/api/books");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_valid_token_carrying_no_role_can_still_read()
    {
        // ReadCatalog asks only for an authenticated user. A provider that has not yet
        // been told about this application's roles should not lock everyone out of the
        // catalogue.
        var response = await Client(TestTokens.WithoutRoles()).GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------- cataloguing is desk work

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task Only_staff_may_add_a_title(string role, HttpStatusCode? expected)
    {
        var response = await Client(TestTokens.For(role))
            .PostAsJsonAsync("/api/books", new { title = "A Title", author = "An Author" });

        AssertGate(response.StatusCode, expected);
    }

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task Only_staff_may_ask_the_model_for_metadata(string role, HttpStatusCode? expected)
    {
        var response = await Client(TestTokens.For(role))
            .PostAsJsonAsync("/api/ai/enrich-metadata", new { title = "Dune", author = "Frank Herbert" });

        AssertGate(response.StatusCode, expected);
    }

    // --------------------------------------------------- deletion is admin only

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task A_librarian_may_catalogue_but_not_delete(string role, HttpStatusCode? expected)
    {
        var response = await Client(TestTokens.For(role))
            .DeleteAsync($"/api/books/{Guid.NewGuid()}");

        AssertGate(response.StatusCode, expected);
    }

    // ------------------------------------------------------- lending is desk work

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task Only_staff_may_lend(string role, HttpStatusCode? expected)
    {
        var client = Client(TestTokens.For(role));
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/loans/checkout",
            new { bookId = Guid.NewGuid(), memberId = Guid.NewGuid() });

        AssertGate(response.StatusCode, expected);
    }

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task Only_staff_may_take_a_return(string role, HttpStatusCode? expected)
    {
        var response = await Client(TestTokens.For(role))
            .PostAsJsonAsync("/api/loans/checkin", new { bookCopyId = Guid.NewGuid() });

        AssertGate(response.StatusCode, expected);
    }

    // ------------------------------------------------------------- privacy

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task A_borrower_cannot_read_the_member_roster(string role, HttpStatusCode? expected)
    {
        // The roster carries every borrower's name and email address.
        var response = await Client(TestTokens.For(role)).GetAsync("/api/members");

        AssertGate(response.StatusCode, expected);
    }

    [Theory]
    [InlineData(LibraryRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(LibraryRole.Librarian, null)]
    [InlineData(LibraryRole.Admin, null)]
    public async Task A_borrower_cannot_see_what_the_whole_library_has_out(
        string role,
        HttpStatusCode? expected)
    {
        var response = await Client(TestTokens.For(role)).GetAsync("/api/loans/open");

        AssertGate(response.StatusCode, expected);
    }

    [Fact]
    public async Task A_borrower_may_see_their_own_loans()
    {
        var response = await Client(TestTokens.Member()).GetAsync("/api/loans/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Asserts either an expected refusal, or that the caller was let past the gate.
    /// What the handler then does is not this test's concern.
    /// </summary>
    private static void AssertGate(HttpStatusCode actual, HttpStatusCode? expectedRefusal)
    {
        if (expectedRefusal is { } refusal)
        {
            Assert.Equal(refusal, actual);
            return;
        }

        Assert.NotEqual(HttpStatusCode.Unauthorized, actual);
        Assert.NotEqual(HttpStatusCode.Forbidden, actual);
    }
}
