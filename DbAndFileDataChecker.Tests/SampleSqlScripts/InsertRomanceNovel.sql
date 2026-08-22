-- ============================================
-- Stored Procedure: InsertRomanceNovel
-- Inserts a novel and auto-resolves author/publisher
-- ============================================

IF OBJECT_ID('dbo.InsertRomanceNovel', 'P') IS NOT NULL
    DROP PROCEDURE dbo.InsertRomanceNovel;
GO

CREATE PROCEDURE dbo.InsertRomanceNovel
(
    @Title NVARCHAR(255),
    @AuthorName NVARCHAR(255),
    @PublicationYear INT,
    @Pages INT,
    @PublisherName NVARCHAR(255),
    @SettingCity NVARCHAR(255),
    @SettingState NVARCHAR(50),
    @Protagonists NVARCHAR(255),
    @RomanceTropes NVARCHAR(255),
    @OneLiner NVARCHAR(MAX)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AuthorId INT;
    DECLARE @PublisherId INT;

    -- Resolve or create Author
    SELECT @AuthorId = AuthorId
    FROM dbo.Authors
    WHERE AuthorName = @AuthorName;

    IF @AuthorId IS NULL
    BEGIN
        INSERT INTO dbo.Authors (AuthorName)
        VALUES (@AuthorName);

        SET @AuthorId = SCOPE_IDENTITY();
    END

    -- Resolve or create Publisher
    SELECT @PublisherId = PublisherId
    FROM dbo.Publishers
    WHERE PublisherName = @PublisherName;

    IF @PublisherId IS NULL
    BEGIN
        INSERT INTO dbo.Publishers (PublisherName)
        VALUES (@PublisherName);

        SET @PublisherId = SCOPE_IDENTITY();
    END

    -- Insert Novel
    INSERT INTO dbo.RomanceNovels
    (
        Title, AuthorId, PublicationYear, Pages,
        PublisherId, SettingCity, SettingState,
        Protagonists, RomanceTropes, OneLiner
    )
    VALUES
    (
        @Title, @AuthorId, @PublicationYear, @Pages,
        @PublisherId, @SettingCity, @SettingState,
        @Protagonists, @RomanceTropes, @OneLiner
    );
END
GO
