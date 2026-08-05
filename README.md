# Athenaeum — Library Management System

A working catalogue, lending desk and returns counter for a small library.

Built as a technical assignment. The brief asked for book management, check-in /
check-out and search; the parts beyond that were chosen because a library that could
not represent three copies of the same book, or that lent the same copy twice under
load, would not survive contact with a real front desk.

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
dotnet test        # 65 unit tests, no database required
```

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
first and simply does not render the button. Everything else works unchanged.

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

**AI cataloguing** — drafts the synopsis, subject, year and keywords from just a
title and author. The suggestion fills the form; a librarian confirms it. Nothing
reaches the catalogue unreviewed.

---

## How it is put together

```
┌─────────────────┐     HTTP/JSON      ┌──────────────────────────────────┐
│  web (Next.js)  │ ─────────────────► │            Library.Api           │
│  React 19 · TS  │                    │   endpoints · ProblemDetails     │
└─────────────────┘                    └────────────────┬─────────────────┘
                                                        │
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
                                       │   entities · rules · policy      │
                                       └──────────────────────────────────┘
```

Dependencies point inwards only. `Library.Domain` references nothing; the use cases
depend on ports, not on EF Core, which is why 65 tests run without a database.

### Three decisions worth explaining

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
instead of blocking on it — so three simultaneous requests for a three-copy title
are served three different copies rather than queueing on one.

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

---

## Where things are

| Concern | File |
|---|---|
| Titles vs. physical copies | [`Book.cs`](src/Library.Domain/Entities/Book.cs) · [`BookCopy.cs`](src/Library.Domain/Entities/BookCopy.cs) |
| Lending terms, fees, grace, cap | [`LoanPolicy.cs`](src/Library.Domain/LoanPolicy.cs) |
| ISBN validation | [`Isbn13.cs`](src/Library.Domain/Isbn13.cs) |
| Atomic, idempotent check-out | [`usp_CheckoutCopy.sql`](src/Library.Infrastructure/Persistence/Sql/usp_CheckoutCopy.sql) |
| Ranked search | [`usp_SearchBooks.sql`](src/Library.Infrastructure/Persistence/Sql/usp_SearchBooks.sql) |
| Correctness indexes | [`LoanConfiguration.cs`](src/Library.Infrastructure/Persistence/Configurations/LoanConfiguration.cs) |
| Calling the procedures | [`LoanRepository.cs`](src/Library.Infrastructure/Persistence/Repositories/LoanRepository.cs) · [`CatalogQueries.cs`](src/Library.Infrastructure/Persistence/Queries/CatalogQueries.cs) |
| Use cases | [`CatalogService.cs`](src/Library.Application/Catalog/CatalogService.cs) · [`LoanService.cs`](src/Library.Application/Loans/LoanService.cs) |
| Retry, backoff, circuit breaker | [`DependencyInjection.cs`](src/Library.Infrastructure/DependencyInjection.cs) |
| LLM client, JSON schema, degradation | [`GeminiMetadataService.cs`](src/Library.Infrastructure/Ai/GeminiMetadataService.cs) |
| Error kind → status code | [`ResultExtensions.cs`](src/Library.Api/ResultExtensions.cs) |
| Idempotency key in the browser | [`app/books/[id]/page.tsx`](web/app/books/[id]/page.tsx) |
| CI, including a real SQL Server | [`ci.yml`](.github/workflows/ci.yml) |

---

## API

| | | |
|---|---|---|
| `GET` | `/api/books` | Search: `query`, `category`, `availableOnly`, `page`, `pageSize` |
| `GET` | `/api/books/{id}` | One title with its copies and their current loans |
| `POST` | `/api/books` | Register a title and its first copies |
| `PUT` | `/api/books/{id}` | Correct a title |
| `DELETE` | `/api/books/{id}` | Remove a title — `409` while a copy is out |
| `POST` | `/api/books/{id}/copies` | Register another physical copy |
| `GET` | `/api/books/categories` | Subjects present in the catalogue |
| `POST` | `/api/loans/checkout` | Borrow a copy — **requires `Idempotency-Key`** |
| `POST` | `/api/loans/checkin` | Return a copy, settle any fee |
| `GET` | `/api/loans/open` | Outstanding loans, soonest due first |
| `GET` | `/api/members` | Active members and how much each holds |
| `GET` | `/api/ai/status` | Whether enrichment is configured |
| `POST` | `/api/ai/enrich-metadata` | Draft metadata for review |

Errors are [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `ProblemDetails`.
`400` invalid · `404` unknown · `409` conflicts with current state · `503` a
dependency is unavailable and a retry may succeed.

Try the idempotency guarantee directly — the same key twice lends one copy:

```bash
curl -i -X POST http://localhost:5080/api/loans/checkout \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-key-1' \
  -d '{"bookId":"<id>","memberId":"<id>"}'
```

---

## Testing

`dotnet test` — 65 unit tests over the domain and application layers, no database
needed.

CI does what unit tests cannot: it starts a real **SQL Server 2022** container,
applies the migrations and procedures, and drives the live endpoints — search, then
a check-out, then *the same idempotency key again*, asserting that the second call
returns the original loan rather than a second one. A mistake in the T-SQL fails the
build rather than the demo.

---

## Known limitations

Stated plainly, because a reviewer will find them anyway.

- **No authentication.** The brief listed SSO as a bonus; the time went into the
  lending core instead. The seams are in place — endpoints are grouped and role
  checks would attach to the groups — but nothing is implemented, and a login page
  that does not actually gate anything would be worse than none.
- **Fees are calculated, not collected.** `CheckinResult.feeCharged` reports what is
  owed; there is no payment or ledger.
- **Members are seeded, not managed.** There is no member CRUD; the desk picks from
  the seeded list.
- **Barcode generation reads the maximum and adds one**, which races. The unique
  index turns a collision into a failed insert rather than two items sharing a
  label, and a real deployment would take these from the label printer's sequence.
- **Migrations run at start-up.** Fine for one instance and for a reviewer who wants
  a single command; several replicas would race, so this belongs in a release step.
- **Search uses `LIKE` with escaped wildcards**, not full-text indexing. Correct and
  predictable at this size; a real catalogue would move to full-text search.
