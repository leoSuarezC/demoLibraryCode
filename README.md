# Athenaeum — Library Management System

A working catalogue, lending desk and returns counter for a small library.

Built as a technical assignment. The brief asked for book management, check-in /
check-out and search; the parts beyond that were chosen because a library that could
not represent three copies of the same book, or that lent the same copy twice under
load, or that let any borrower read every other borrower's address, would not survive
contact with a real front desk.

---

## Running it

### With Docker (one command, nothing else installed)

```bash
docker compose up --build
```

| | |
|---|---|
| Web | <http://localhost:3000> |
| API | <http://localhost:5080> |
| OpenAPI | <http://localhost:5080/openapi/v1.json> |

Migrations, stored procedures and seed data are applied on start-up. The catalogue
arrives with 18 titles, 34 copies, 6 members and several open loans — two of them
deliberately overdue, so the overdue path is visible without waiting three weeks.

First run takes a few minutes: SQL Server has to come up before the API will start.
On an Apple Silicon Mac, enable **Rosetta for x86/amd64 emulation** in Docker Desktop
(Settings → General) — Microsoft ships no arm64 build of the SQL Server image.

### Without Docker

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node 20+](https://nodejs.org)
and a reachable SQL Server (LocalDB, Express, a container, or Azure SQL).

```bash
# 1. Point the API at your SQL Server (the default targets LocalDB)
#    src/Library.Api/appsettings.json → ConnectionStrings:LibraryDb

# 2. API — creates the database on first run
dotnet run --project src/Library.Api        # http://localhost:5080

# 3. Web, in a second terminal
cd web
cp .env.example .env.local
npm install && npm run dev                  # http://localhost:3000
```

```bash
dotnet test        # 105 tests, no database required
```

### Choosing a frontend: Next.js or plain React (Vite)

Two interchangeable clients ship with the repo. They talk to the same API and share
the same API client, types, styling and components, so they render identically — run
whichever you prefer.

| Frontend | Stack | Folder | Dev URL |
|---|---|---|---|
| `web` | Next.js (App Router) · React 19 · SSR | [`web/`](web) | <http://localhost:3000> |
| `web-vite` | Plain React 19 · Vite · React Router · SPA | [`web-vite/`](web-vite) | <http://localhost:5173> |

```bash
# Plain-React SPA, in a second terminal (the API from step 2 must be running)
cd web-vite
cp .env.example .env             # VITE_API_URL defaults to http://localhost:5080
npm install && npm run dev       # http://localhost:5173
```

Both origins (`:3000` and `:5173`) are allow-listed in `Cors:AllowedOrigins`, so
either runs against the API unchanged. Only the routing layer differs between them:
`web` uses the Next.js App Router, `web-vite` uses React Router. See
[`web-vite/README.md`](web-vite/README.md) for how the port is structured.

---

## Signing in

The sign-in screen offers every seeded account. **Look at the library through more
than one of them** — the difference is the point.

| Account | Role | Can |
|---|---|---|
| Amara Okafor | **Admin** | Everything, including removing titles |
| Sam Reyes | **Librarian** | Catalogue, lend, take returns — but no Remove button anywhere |
| Ada Lovelace *(and 5 more)* | **Member** | Search and see their own loans. No lending, no roster, no library-wide loan list |

Sign in as Sam and the Remove button is gone. Sign in as Ada and so are the lending
panel, the member roster, the "On loan" page and the ability to add a book — and
`GET /api/members` answers `403` if you try it directly.

### Using a real identity provider

The API is an ordinary **OAuth2 resource server**: it validates bearer tokens and
reads roles from them. It stores no passwords and does not care who issued the token.
One setting switches it to Auth0, Microsoft Entra ID, Clerk, Okta or Keycloak:

```bash
OIDC_AUTHORITY=https://your-tenant.eu.auth0.com/ docker compose up
```

Signing keys are then discovered from the provider's `.well-known` document and
refreshed on rotation. Setting an authority **disables the local account picker
entirely** — `/api/auth/token` starts answering `404`, so a deployment wired to a real
provider cannot also accept hand-issued tokens.

<details>
<summary>Why there is a local sign-in at all</summary>

Requiring a reviewer to register an Auth0 tenant before anything works would make
authentication a barrier rather than a feature. The local issuer mints ordinary HS256
JWTs carrying the same claims a real provider sends, so the rest of the API cannot
tell the difference — which is precisely what makes the swap a configuration change
and nothing more. It verifies no credential, because there is none to verify, and it
is refused the moment a real authority is configured.

</details>

### Enabling the AI feature (optional)

Get a free key from [Google AI Studio](https://aistudio.google.com) — no card, no cost —
and supply it as `Gemini__ApiKey`:

```bash
# Local
dotnet user-secrets set "Gemini:ApiKey" "<your-key>" --project src/Library.Api

# Docker
GEMINI_API_KEY=<your-key> docker compose up --build
```

Without a key the feature is **hidden**, not broken: the UI asks `/api/ai/status`
first and simply does not render the button.

---

## What it does

**Catalogue** — add, edit and remove titles. ISBNs are normalised and check-digit
validated on entry, so a transposed digit is caught at the desk rather than
discovered months later.

**Copies** — a title holds any number of physical items, each with its own barcode
and status. This is the difference between a demo and something a library could use:
`IsCheckedOut` on a book cannot express "we own three, two are out".

**Lending** — borrow and return by copy, with a 21-day term, a five-item limit per
member, a one-day grace period and a capped overdue fee.

**Search** — ranked across title, author, ISBN, subject and keywords, filterable by
subject and availability, paged.

**Roles** — three, because a small library has three kinds of person.

**AI cataloguing** — drafts the synopsis, subject, year and keywords from just a
title and author. The suggestion fills the form; a librarian confirms it. Nothing
reaches the catalogue unreviewed.

---

## How it is put together

```
┌─────────────────┐   HTTP/JSON + Bearer   ┌──────────────────────────────────┐
│  web (Next.js)  │ ─────────────────────► │            Library.Api           │
│  web-vite (SPA) │                        │  endpoints · policies · JWT      │
│  React 19 · TS  │                        └────────────────┬─────────────────┘
└─────────────────┘                                         │
                                           ┌────────────────▼─────────────────┐
                                           │       Library.Application        │
                                           │   use cases · ports · DTOs       │
                                           └────────────────┬─────────────────┘
                                                            │
                            ┌───────────────────────────────▼─────────────────┐
                            │              Library.Infrastructure             │
                            │   EF Core · T-SQL procedures · Gemini client    │
                            └───────────────────────────────┬─────────────────┘
                                                            │
                                           ┌────────────────▼─────────────────┐
                                           │         Library.Domain           │
                                           │  entities · rules · roles        │
                                           └──────────────────────────────────┘
```

Dependencies point inwards only. `Library.Domain` references nothing; the use cases
depend on ports, not on EF Core, which is why the tests run without a database.

### Four decisions worth explaining

**Check-out is a stored procedure, not application code.**

Selecting an available copy and then marking it lent is a read-modify-write. Split
across two round trips it leaves a window where a second request reads the same copy
as available — and under load that window is hit. `usp_CheckoutCopy` does the whole
thing in one transaction:

```sql
SELECT TOP (1) @CopyId = Id
FROM dbo.BookCopies WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE BookId = @BookId AND Status = 0
ORDER BY Barcode;
```

`UPDLOCK` claims the row so no one else can select it for the same purpose.
`READPAST` makes a competing request skip a locked row and take the next free copy
instead of blocking on it — so three simultaneous requests for a three-copy title are
served three different copies rather than queueing on one.

**Retries cannot lend twice.**

Check-out requires an `Idempotency-Key` header. The first thing the procedure does is
look for that key; the guarantee, though, is the unique index — two truly concurrent
retries can both find nothing, but only one can win `UX_Loans_IdempotencyKey`. The
duplicate-key violation is caught and reported as *already processed*, because the
caller's intent was satisfied either way. A replay answers `200` with the original
loan plus `Idempotent-Replay: true`.

**Some indexes are correctness, not tuning.**

`UX_Loans_OpenLoanPerCopy` is unique on `BookCopyId` filtered to `ReturnedAtUtc IS NULL`:
any number of closed loans per copy, never two open ones. If a bug ever reaches the
check-out path, the database refuses rather than quietly double-lending a book.

**Authorisation is about privacy, not just capability.**

Two of the rules exist to protect people rather than data integrity. `/api/members`
is desk-staff only, because the roster carries every borrower's name and email
address. `/api/loans/mine` takes the member id from the validated token and offers no
parameter for it — a borrower cannot request somebody else's loans because there is
no way to ask.

Endpoints reference named policies, never role names, so "who may lend a book" is
answered in one place. Each route group states its floor and individual routes raise
it, so an endpoint added to a group is protected by default rather than public by
accident.

---

## Where things are

| Concern | File |
|---|---|
| Titles vs. physical copies | [`Book.cs`](src/Library.Domain/Entities/Book.cs) · [`BookCopy.cs`](src/Library.Domain/Entities/BookCopy.cs) |
| Lending terms, fees, grace, cap | [`LoanPolicy.cs`](src/Library.Domain/LoanPolicy.cs) |
| Roles and policy names | [`LibraryRole.cs`](src/Library.Domain/LibraryRole.cs) |
| ISBN validation | [`Isbn13.cs`](src/Library.Domain/Isbn13.cs) |
| Atomic, idempotent check-out | [`usp_CheckoutCopy.sql`](src/Library.Infrastructure/Persistence/Sql/usp_CheckoutCopy.sql) |
| Ranked search | [`usp_SearchBooks.sql`](src/Library.Infrastructure/Persistence/Sql/usp_SearchBooks.sql) |
| Correctness indexes | [`LoanConfiguration.cs`](src/Library.Infrastructure/Persistence/Configurations/LoanConfiguration.cs) |
| Calling the procedures | [`LoanRepository.cs`](src/Library.Infrastructure/Persistence/Repositories/LoanRepository.cs) · [`CatalogQueries.cs`](src/Library.Infrastructure/Persistence/Queries/CatalogQueries.cs) |
| Use cases | [`CatalogService.cs`](src/Library.Application/Catalog/CatalogService.cs) · [`LoanService.cs`](src/Library.Application/Loans/LoanService.cs) |
| JWT validation, role mapping, policies | [`AuthenticationSetup.cs`](src/Library.Api/Auth/AuthenticationSetup.cs) |
| Local sign-in (dev only) | [`DevelopmentTokenIssuer.cs`](src/Library.Api/Auth/DevelopmentTokenIssuer.cs) |
| Retry, backoff, circuit breaker | [`DependencyInjection.cs`](src/Library.Infrastructure/DependencyInjection.cs) |
| LLM client, JSON schema, degradation | [`GeminiMetadataService.cs`](src/Library.Infrastructure/Ai/GeminiMetadataService.cs) |
| Error kind → status code | [`ResultExtensions.cs`](src/Library.Api/ResultExtensions.cs) |
| Access-control matrix, as tests | [`EndpointAuthorizationTests.cs`](tests/Library.UnitTests/Authorization/EndpointAuthorizationTests.cs) |
| Idempotency key in the browser | [`app/books/[id]/page.tsx`](web/app/books/[id]/page.tsx) |
| CI, including a real SQL Server | [`ci.yml`](.github/workflows/ci.yml) |

---

## API

Everything except `/health` and the two `/api/auth` discovery routes requires a bearer
token.

| | | | Requires |
|---|---|---|---|
| `GET` | `/api/books` | Search: `query`, `category`, `availableOnly`, `page`, `pageSize` | signed in |
| `GET` | `/api/books/{id}` | One title with its copies and their current loans | signed in |
| `POST` | `/api/books` | Register a title and its first copies | Librarian |
| `PUT` | `/api/books/{id}` | Correct a title | Librarian |
| `DELETE` | `/api/books/{id}` | Remove a title — `409` while a copy is out | **Admin** |
| `POST` | `/api/books/{id}/copies` | Register another physical copy | Librarian |
| `GET` | `/api/books/categories` | Subjects present in the catalogue | signed in |
| `POST` | `/api/loans/checkout` | Borrow a copy — **requires `Idempotency-Key`** | Librarian |
| `POST` | `/api/loans/checkin` | Return a copy, settle any fee | Librarian |
| `GET` | `/api/loans/open` | Outstanding loans across the library | Librarian |
| `GET` | `/api/loans/mine` | What the signed-in member is holding | signed in |
| `GET` | `/api/members` | Active members and how much each holds | Librarian |
| `GET` | `/api/ai/status` | Whether enrichment is configured | Librarian |
| `POST` | `/api/ai/enrich-metadata` | Draft metadata for review | Librarian |
| `GET` | `/api/auth/config` | How this deployment authenticates | anonymous |
| `GET` | `/api/auth/me` | Who the current token belongs to | signed in |

*Librarian* means Librarian or Admin throughout.

Errors are [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `ProblemDetails`.
`400` invalid · `401` no usable token · `403` role insufficient · `404` unknown ·
`409` conflicts with current state · `503` a dependency is unavailable.

Try the idempotency guarantee directly — the same key twice lends one copy:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"accountId":"staff-librarian"}' | jq -r .accessToken)

curl -i -X POST http://localhost:5080/api/loans/checkout \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-key-1' \
  -d '{"bookId":"<id>","memberId":"<id>"}'
```

---

## Testing

`dotnet test` — **105 tests**, no database needed.

- **Domain and use cases** run against substituted ports, which is what those ports
  were introduced for.
- **Authorisation** runs the real `Program` under `WebApplicationFactory` with real
  signed JWTs, asserting the status code for every role against every endpoint. Not a
  stub authentication handler: a stub asserts an identity and skips exactly the part
  most likely to be misconfigured. This suite earned its keep immediately — it caught
  a bug where the role-mapping event enumerated a claim collection while adding to it,
  which threw on every token that actually carried a role.

CI does what unit tests cannot: it starts a real **SQL Server 2022** container,
applies the migrations and procedures, and drives the live endpoints — anonymous
access is refused, a librarian is refused a delete, then search, a check-out, *the
same idempotency key again*, a borrower being refused the roster, and a check-in. A
mistake in the T-SQL fails the build rather than the demo.

The same script runs by hand against anything already up, so a reviewer can watch it
rather than take CI's word for it:

```bash
docker compose up --build            # one terminal
pwsh scripts/smoke-test.ps1          # another, once the API answers
```

Two of its assertions are the ones worth watching. **The same idempotency key twice**
must return the same loan and lend one copy, not two. **Three simultaneous check-outs**
of the same title must take three *different* copies — that is `usp_CheckoutCopy`'s
`UPDLOCK, READPAST` doing its job, and it is the assertion that fails if the locking
hints are ever "tidied up".

---

## Known limitations

Stated plainly, because a reviewer will find them anyway.

- **Fees are calculated, not collected.** `CheckinResult.feeCharged` reports what is
  owed; there is no payment or ledger.
- **Members are seeded, not managed.** There is no member CRUD, and no self-service
  registration — a librarian would enrol borrowers in a system that does not exist yet.
- **User accounts are not linked to member records** except through the `member_id`
  claim the token carries. With a real provider that mapping would live in a table.
- **Barcode generation reads the maximum and adds one**, which races. The unique
  index turns a collision into a failed insert rather than two items sharing a label,
  and a real deployment would take these from the label printer's sequence.
- **Migrations run at start-up.** Fine for one instance and for a reviewer who wants
  a single command; several replicas would race, so this belongs in a release step.
- **Search uses `LIKE` with escaped wildcards**, not full-text indexing. Correct and
  predictable at this size; a real catalogue would move to full-text search.
- **No refresh tokens.** The demo session lasts eight hours and ends with the tab.
  Against a real provider the frontend would hold the access token in memory and the
  refresh token in an HttpOnly cookie.
