-- ============================================
-- Drop existing tables if they exist
-- ============================================

IF OBJECT_ID('dbo.RomanceNovels', 'U') IS NOT NULL DROP TABLE dbo.RomanceNovels;
IF OBJECT_ID('dbo.Authors', 'U') IS NOT NULL DROP TABLE dbo.Authors;
IF OBJECT_ID('dbo.Publishers', 'U') IS NOT NULL DROP TABLE dbo.Publishers;
GO

-- ============================================
-- Authors Table
-- ============================================

CREATE TABLE dbo.Authors
(
    AuthorId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AuthorName NVARCHAR(255) NOT NULL,
    Hometown NVARCHAR(255) NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

-- ============================================
-- Publishers Table
-- ============================================

CREATE TABLE dbo.Publishers
(
    PublisherId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PublisherName NVARCHAR(255) NOT NULL,
    HeadquartersCity NVARCHAR(255) NULL,
    HeadquartersState NVARCHAR(50) NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

-- ============================================
-- RomanceNovels Table (Updated with FKs)
-- ============================================

CREATE TABLE dbo.RomanceNovels
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Title NVARCHAR(255) NOT NULL,
    AuthorId INT NOT NULL,
    PublicationYear INT NOT NULL,
    Pages INT NOT NULL,
    PublisherId INT NOT NULL,
    SettingCity NVARCHAR(255) NOT NULL,
    SettingState NVARCHAR(50) NOT NULL,
    Protagonists NVARCHAR(255) NOT NULL,
    RomanceTropes NVARCHAR(255) NOT NULL,
    OneLiner NVARCHAR(MAX) NOT NULL,

    CONSTRAINT FK_RomanceNovels_Authors
        FOREIGN KEY (AuthorId) REFERENCES dbo.Authors(AuthorId),

    CONSTRAINT FK_RomanceNovels_Publishers
        FOREIGN KEY (PublisherId) REFERENCES dbo.Publishers(PublisherId)
);
GO
