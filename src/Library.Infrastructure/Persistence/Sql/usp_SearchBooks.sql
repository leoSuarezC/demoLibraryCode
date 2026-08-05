/*
    usp_SearchBooks
    ---------------
    Ranked, paginated catalogue search across title, author, ISBN, category and tags.

    Why this is a stored procedure:

    Availability is an aggregate over a child table, and a librarian searching for
    "le guin" expects the author's books ranked above a title that merely mentions
    her. Expressing "match, count copies, score, order, page" as one plan lets the
    optimiser see the whole job. Composed in LINQ it becomes either several round
    trips or one query nobody can read.

    Ranking is deliberately coarse. Exact and prefix matches on the fields a person
    actually types beat incidental matches inside a synopsis, which is enough to put
    the obvious answer first without pretending to be a search engine.

    OPTION (RECOMPILE):

    Every filter here is optional. One cached plan cannot serve both "no filters,
    return the first page of everything" and "one narrow term plus a category". The
    recompile costs a few milliseconds per call and buys a plan that matches the
    arguments actually supplied - the standard trade for this shape of query.

    @TotalCount is returned as an output parameter so the caller gets the page and the
    total in a single round trip.
*/
CREATE OR ALTER PROCEDURE dbo.usp_SearchBooks
    @Query         NVARCHAR(400) = NULL,
    @Category      NVARCHAR(100) = NULL,
    @AvailableOnly BIT           = 0,
    @Skip          INT           = 0,
    @Take          INT           = 20,
    @TotalCount    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Escape LIKE wildcards so a search for "100%" is a literal search, not a
    -- pattern that matches the whole catalogue.
    DECLARE @Pattern NVARCHAR(410) =
        N'%' + REPLACE(REPLACE(REPLACE(@Query, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%';

    DECLARE @Prefix NVARCHAR(410) =
        REPLACE(REPLACE(REPLACE(@Query, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%';

    CREATE TABLE #Matches
    (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Title           NVARCHAR(400)    NOT NULL,
        Author          NVARCHAR(300)    NOT NULL,
        Isbn            NVARCHAR(13)     NULL,
        Category        NVARCHAR(100)    NULL,
        PublishedYear   INT              NULL,
        TotalCopies     INT              NOT NULL,
        AvailableCopies INT              NOT NULL,
        Relevance       INT              NOT NULL
    );

    INSERT INTO #Matches
        (Id, Title, Author, Isbn, Category, PublishedYear, TotalCopies, AvailableCopies, Relevance)
    SELECT
        b.Id,
        b.Title,
        b.Author,
        b.Isbn,
        b.Category,
        b.PublishedYear,
        a.TotalCopies,
        a.AvailableCopies,
        Relevance = CASE
            WHEN @Query IS NULL                  THEN 0
            WHEN b.Title  =    @Query            THEN 100  -- exact title
            WHEN b.Isbn   =    @Query            THEN 95   -- scanned barcode / pasted ISBN
            WHEN b.Title  LIKE @Prefix           THEN 80   -- title starts with
            WHEN b.Author LIKE @Prefix           THEN 70   -- author starts with
            WHEN b.Title  LIKE @Pattern          THEN 60   -- title contains
            WHEN b.Author LIKE @Pattern          THEN 50   -- author contains
            WHEN b.Category LIKE @Pattern        THEN 30
            WHEN b.Tags   LIKE @Pattern          THEN 20
            ELSE 10                                        -- matched somewhere else
        END
    FROM dbo.Books AS b
    CROSS APPLY
    (
        SELECT
            TotalCopies     = COUNT(c.Id),
            AvailableCopies = ISNULL(SUM(CASE WHEN c.Status = 0 THEN 1 ELSE 0 END), 0)
        FROM dbo.BookCopies AS c
        WHERE c.BookId = b.Id
    ) AS a
    WHERE
        (@Category IS NULL OR b.Category = @Category)
        AND (@AvailableOnly = 0 OR a.AvailableCopies > 0)
        AND
        (
            @Query IS NULL
            OR b.Title    LIKE @Pattern
            OR b.Author   LIKE @Pattern
            OR b.Isbn     LIKE @Pattern
            OR b.Category LIKE @Pattern
            OR b.Tags     LIKE @Pattern
        )
    OPTION (RECOMPILE);

    SELECT @TotalCount = COUNT(*) FROM #Matches;

    SELECT
        Id,
        Title,
        Author,
        Isbn,
        Category,
        PublishedYear,
        TotalCopies,
        AvailableCopies
    FROM #Matches
    ORDER BY
        Relevance DESC,
        Title ASC          -- stable tie-break, so paging cannot repeat or skip a row
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY;

    DROP TABLE #Matches;
END
