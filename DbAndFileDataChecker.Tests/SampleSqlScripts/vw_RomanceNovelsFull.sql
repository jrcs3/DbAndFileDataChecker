-- ============================================
-- View: vw_RomanceNovelsFull
-- Brings Authors, Publishers, and RomanceNovels together
-- ============================================

IF OBJECT_ID('dbo.vw_RomanceNovelsFull', 'V') IS NOT NULL
    DROP VIEW dbo.vw_RomanceNovelsFull;
GO

CREATE VIEW dbo.vw_RomanceNovelsFull
AS
SELECT
    rn.Id AS NovelId,
    rn.Title,
    rn.PublicationYear,
    rn.Pages,
    rn.SettingCity,
    rn.SettingState,
    rn.Protagonists,
    rn.RomanceTropes,
    rn.OneLiner,

    -- Author fields
    a.AuthorId,
    a.AuthorName,
    a.Hometown AS AuthorHometown,
    a.Notes AS AuthorNotes,

    -- Publisher fields
    p.PublisherId,
    p.PublisherName,
    p.HeadquartersCity,
    p.HeadquartersState,
    p.Notes AS PublisherNotes

FROM dbo.RomanceNovels rn
INNER JOIN dbo.Authors a
    ON rn.AuthorId = a.AuthorId
INNER JOIN dbo.Publishers p
    ON rn.PublisherId = p.PublisherId;
GO
