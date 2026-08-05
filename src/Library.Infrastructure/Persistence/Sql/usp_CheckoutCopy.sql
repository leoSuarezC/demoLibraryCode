/*
    usp_CheckoutCopy
    ----------------
    Lends one available copy of a title and opens the corresponding loan.

    Why this is a stored procedure rather than application code:

    Choosing a free copy and then marking it lent is a read-modify-write. Split
    across two round trips it has a window in which a second request reads the same
    copy as available - and under any real load that window is hit. Doing the work
    in one place, inside a transaction, with an update lock held on the chosen row,
    closes it.

    Concurrency strategy:

        WITH (UPDLOCK, READPAST, ROWLOCK)

    UPDLOCK takes an update lock at read time, so no other transaction can select the
    same copy for the same purpose. READPAST makes a competing request skip rows that
    are already locked and take the next free copy instead of blocking behind it. For
    a title with three copies, three simultaneous requests are served three different
    copies rather than queueing on one.

    Idempotency:

    The caller supplies a key. The first check is a cheap lookup; the authoritative
    guarantee is the unique index UX_Loans_IdempotencyKey, whose violation is caught
    below and reported as "already processed". Two truly simultaneous retries can both
    pass the lookup, but only one can pass the index.

    @Outcome values mirror Library.Application.Abstractions.CheckoutOutcome:
        0 Created   1 AlreadyProcessed   2 NoCopyAvailable
        3 LoanLimitReached   4 BookNotFound   5 MemberNotFound
*/
CREATE OR ALTER PROCEDURE dbo.usp_CheckoutCopy
    @BookId             UNIQUEIDENTIFIER,
    @MemberId           UNIQUEIDENTIFIER,
    @IdempotencyKey     NVARCHAR(100),
    @LoanDays           INT,
    @MaxConcurrentLoans INT,
    @Outcome            INT OUTPUT,
    @LoanId             UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Outcome = 0;
    SET @LoanId  = NULL;

    -- Fast path: this exact request has already been served.
    SELECT @LoanId = Id
    FROM dbo.Loans
    WHERE IdempotencyKey = @IdempotencyKey;

    IF @LoanId IS NOT NULL
    BEGIN
        SET @Outcome = 1;   -- AlreadyProcessed
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Id = @BookId)
    BEGIN
        SET @Outcome = 4;   -- BookNotFound
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Members WHERE Id = @MemberId AND IsActive = 1)
    BEGIN
        SET @Outcome = 5;   -- MemberNotFound
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @OpenLoans INT;

        SELECT @OpenLoans = COUNT(*)
        FROM dbo.Loans
        WHERE MemberId = @MemberId
          AND ReturnedAtUtc IS NULL;

        IF @OpenLoans >= @MaxConcurrentLoans
        BEGIN
            ROLLBACK TRANSACTION;
            SET @Outcome = 3;   -- LoanLimitReached
            RETURN;
        END

        DECLARE @CopyId UNIQUEIDENTIFIER;

        -- Claim a copy. See the concurrency note in the header for the lock hints.
        SELECT TOP (1) @CopyId = Id
        FROM dbo.BookCopies WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE BookId = @BookId
          AND Status = 0               -- CopyStatus.Available
        ORDER BY Barcode;

        IF @CopyId IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SET @Outcome = 2;   -- NoCopyAvailable
            RETURN;
        END

        UPDATE dbo.BookCopies
        SET Status = 1                 -- CopyStatus.OnLoan
        WHERE Id = @CopyId;

        DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
        SET @LoanId = NEWID();

        INSERT INTO dbo.Loans
            (Id, BookCopyId, MemberId, CheckedOutAtUtc, DueAtUtc, ReturnedAtUtc, IdempotencyKey)
        VALUES
            (@LoanId, @CopyId, @MemberId, @Now, DATEADD(DAY, @LoanDays, @Now), NULL, @IdempotencyKey);

        COMMIT TRANSACTION;

        SET @Outcome = 0;   -- Created
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        -- 2601/2627: a concurrent retry carrying the same idempotency key won the
        -- race to the unique index. That is a success for the caller, not an error -
        -- their loan exists, it was simply created by the other request.
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SELECT @LoanId = Id
            FROM dbo.Loans
            WHERE IdempotencyKey = @IdempotencyKey;

            IF @LoanId IS NOT NULL
            BEGIN
                SET @Outcome = 1;   -- AlreadyProcessed
                RETURN;
            END
        END

        THROW;
    END CATCH
END
