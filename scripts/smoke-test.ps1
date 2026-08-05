#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Drives a running Athenaeum API and asserts the behaviour that unit tests cannot reach.

.DESCRIPTION
    Unit tests substitute the ports, so they say nothing about whether the stored
    procedures are correct, or whether they even parse. This exercises the live system
    against a real SQL Server:

        the catalogue is not public
        a librarian cannot delete
        ranked search                      (usp_SearchBooks)
        check-out                          (usp_CheckoutCopy)
        the same idempotency key twice     lends one copy, not two
        concurrent check-outs              take different copies, not the same one
        a borrower cannot read the roster
        a borrower can see their own loans
        check-in

    Run by CI after `docker compose up`, and by hand against a local instance:

        pwsh scripts/smoke-test.ps1 -ApiUrl http://localhost:5080

.PARAMETER ApiUrl
    Base address of a running API.

.PARAMETER TimeoutSeconds
    How long to wait for the API to report healthy before giving up.
#>
[CmdletBinding()]
param(
    [string]$ApiUrl = "http://localhost:5080",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$script:Failures = 0

function Step($message) { Write-Host "`n── $message" -ForegroundColor Cyan }
function Pass($message) { Write-Host "   PASS  $message" -ForegroundColor Green }

function Fail($message) {
    Write-Host "   FAIL  $message" -ForegroundColor Red
    $script:Failures++
}

function Assert($condition, $message) {
    if ($condition) { Pass $message } else { Fail $message }
}

<#
    Returns the status code without throwing, so a refusal can be asserted rather than
    caught. Invoke-RestMethod treats 4xx as terminating, which is unhelpful when the
    4xx is the thing under test.
#>
function Get-StatusCode {
    param($Method, $Uri, $Token, $Body)

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    try {
        $params = @{
            Method      = $Method
            Uri         = $Uri
            Headers     = $headers
            ErrorAction = "Stop"
        }
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Compress)
            $params.ContentType = "application/json"
        }
        (Invoke-WebRequest @params -SkipHttpErrorCheck).StatusCode
    }
    catch {
        if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { -1 }
    }
}

function Invoke-Api {
    param($Method, $Path, $Token, $Body, $IdempotencyKey)

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }

    $params = @{
        Method      = $Method
        Uri         = "$ApiUrl$Path"
        Headers     = $headers
        ErrorAction = "Stop"
    }
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Compress)
        $params.ContentType = "application/json"
    }

    Invoke-RestMethod @params
}

function Get-Token($accountId) {
    (Invoke-Api -Method POST -Path "/api/auth/token" -Body @{ accountId = $accountId }).accessToken
}

# ---------------------------------------------------------------- wait for start-up

Step "Waiting for $ApiUrl to report healthy"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$healthy = $false

# The API applies migrations, creates the procedures and seeds before it starts
# listening, so a first run against an empty SQL Server takes a while. The sleep
# sits outside the catch on purpose: an API that answers but is not yet healthy
# would otherwise spin this loop flat out until the deadline.
while ((Get-Date) -lt $deadline) {
    try {
        if ((Invoke-RestMethod "$ApiUrl/health" -TimeoutSec 5).status -eq "healthy") {
            $healthy = $true
            break
        }
    }
    catch { }
    Start-Sleep -Seconds 2
}

if (-not $healthy) {
    Write-Host "The API never became healthy within $TimeoutSeconds seconds." -ForegroundColor Red
    exit 1
}
Pass "healthy"

# ------------------------------------------------------------------ authentication

Step "The catalogue is not public"
Assert ((Get-StatusCode -Method GET -Uri "$ApiUrl/api/books") -eq 401) `
    "anonymous GET /api/books is refused with 401"

Step "Signing in"
$librarian = Get-Token "staff-librarian"
$admin = Get-Token "staff-admin"
Assert ($librarian -and $admin) "tokens issued for staff accounts"

Step "A librarian may catalogue but not delete"
$fakeId = [guid]::NewGuid()
Assert ((Get-StatusCode -Method DELETE -Uri "$ApiUrl/api/books/$fakeId" -Token $librarian) -eq 403) `
    "librarian DELETE is refused with 403"
Assert ((Get-StatusCode -Method DELETE -Uri "$ApiUrl/api/books/$fakeId" -Token $admin) -ne 403) `
    "admin DELETE is not refused by policy"

# ------------------------------------------------------------- usp_SearchBooks

Step "Ranked search (usp_SearchBooks)"
$byAuthor = Invoke-Api -Method GET -Path "/api/books?query=le%20guin" -Token $librarian
Assert ($byAuthor.totalCount -gt 0) "author search returns $($byAuthor.totalCount) matches"
Assert ($byAuthor.items[0].author -like "*Le Guin*") `
    "the top hit is by the author searched for, not an incidental match"

$byTitle = Invoke-Api -Method GET -Path "/api/books?query=Dune" -Token $librarian
Assert ($byTitle.items[0].title -eq "Dune") "an exact title match ranks first"

$paged = Invoke-Api -Method GET -Path "/api/books?pageSize=5" -Token $librarian
Assert ($paged.items.Count -eq 5 -and $paged.totalCount -gt 5) `
    "paging returns 5 of $($paged.totalCount)"

$wildcard = Invoke-Api -Method GET -Path "/api/books?query=%25" -Token $librarian
Assert ($wildcard.totalCount -eq 0) `
    "a literal % is escaped rather than matching the whole catalogue"

$available = Invoke-Api -Method GET -Path "/api/books?availableOnly=true" -Token $librarian
Assert ($available.totalCount -gt 0) "availability filter returns $($available.totalCount) titles"

# ------------------------------------------------------------ usp_CheckoutCopy

Step "Check-out (usp_CheckoutCopy)"
$book = $available.items[0]
$members = Invoke-Api -Method GET -Path "/api/members" -Token $librarian
$member = $members[0]
$key = "smoke-$([guid]::NewGuid())"

$first = Invoke-Api -Method POST -Path "/api/loans/checkout" -Token $librarian `
    -IdempotencyKey $key -Body @{ bookId = $book.id; memberId = $member.id }

Assert (-not $first.wasAlreadyProcessed) "a first check-out is not reported as a replay"
Assert ($first.loan.barcode) "copy $($first.loan.barcode) lent to $($first.loan.memberName)"

Step "The same idempotency key must not lend a second copy"
$second = Invoke-Api -Method POST -Path "/api/loans/checkout" -Token $librarian `
    -IdempotencyKey $key -Body @{ bookId = $book.id; memberId = $member.id }

Assert ($second.wasAlreadyProcessed) "the retry is reported as already processed"
Assert ($second.loan.id -eq $first.loan.id) "the retry returns the original loan"
Assert ($second.loan.barcode -eq $first.loan.barcode) "no second copy left the shelf"

# --------------------------------------------------------------- concurrency

Step "Concurrent check-outs of one title take different copies (UPDLOCK, READPAST)"
$multi = (Invoke-Api -Method GET -Path "/api/books?availableOnly=true&pageSize=50" -Token $librarian).items |
    Where-Object { $_.availableCopies -ge 3 } |
    Select-Object -First 1

if (-not $multi) {
    Write-Host "   SKIP  no title with 3 free copies in the current state" -ForegroundColor Yellow
}
else {
    <#
        HttpClient rather than background jobs. Jobs each pay process or runspace
        start-up before their request leaves, which staggers them by enough that they
        may never actually contend - and a concurrency test that does not overlap
        proves nothing. Every SendAsync here is in flight before any of them is awaited.
    #>
    Add-Type -AssemblyName System.Net.Http

    $client = [System.Net.Http.HttpClient]::new()
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $librarian)

    $borrowers = @($members | Select-Object -First 3)
    $tasks = @()

    foreach ($b in $borrowers) {
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::Post, "$ApiUrl/api/loans/checkout")
        $request.Content = [System.Net.Http.StringContent]::new(
        (@{ bookId = $multi.id; memberId = $b.id } | ConvertTo-Json -Compress),
            [System.Text.Encoding]::UTF8, "application/json")
        $request.Headers.Add("Idempotency-Key", [guid]::NewGuid().ToString())

        $tasks += $client.SendAsync($request)
    }

    [System.Threading.Tasks.Task]::WaitAll($tasks, 60000) | Out-Null

    $barcodes = @()
    foreach ($task in $tasks) {
        if (-not $task.IsCompleted) { continue }
        $response = $task.Result
        if (-not $response.IsSuccessStatusCode) { continue }
        $payload = $response.Content.ReadAsStringAsync().Result | ConvertFrom-Json
        $barcodes += $payload.loan.barcode
    }
    $client.Dispose()

    $distinct = @($barcodes | Select-Object -Unique)

    Assert ($barcodes.Count -gt 1) `
        "$($barcodes.Count) of 3 simultaneous check-outs succeeded"
    Assert ($barcodes.Count -eq $distinct.Count) `
        "they took $($distinct.Count) distinct copies - no copy was lent twice"
}

# ------------------------------------------------------------------- privacy

Step "A borrower sees only their own affairs"
$memberToken = Get-Token "member-$($member.id)"

Assert ((Get-StatusCode -Method GET -Uri "$ApiUrl/api/members" -Token $memberToken) -eq 403) `
    "borrower is refused the member roster"
Assert ((Get-StatusCode -Method GET -Uri "$ApiUrl/api/loans/open" -Token $memberToken) -eq 403) `
    "borrower is refused the library-wide loan list"

$mine = Invoke-Api -Method GET -Path "/api/loans/mine" -Token $memberToken
Assert ($null -ne $mine) "borrower can read their own loans ($($mine.Count) out)"

# ------------------------------------------------------------------ check-in

Step "Check-in"
$returned = Invoke-Api -Method POST -Path "/api/loans/checkin" -Token $librarian `
    -Body @{ bookCopyId = $first.loan.bookCopyId }

Assert ($returned.loan.returnedAtUtc) "loan closed at $($returned.loan.returnedAtUtc)"
Assert ((Get-StatusCode -Method POST -Uri "$ApiUrl/api/loans/checkin" -Token $librarian `
            -Body @{ bookCopyId = $first.loan.bookCopyId }) -eq 404) `
    "returning the same copy twice is refused"

# -------------------------------------------------------------------- verdict

Write-Host ""
if ($script:Failures -eq 0) {
    Write-Host "All smoke tests passed." -ForegroundColor Green
    exit 0
}

Write-Host "$($script:Failures) smoke test(s) failed." -ForegroundColor Red
exit 1
