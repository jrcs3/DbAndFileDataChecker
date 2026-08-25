using NUnit.Framework.Legacy;
using System.Data.Common;
using System.Text;

namespace DbAndFileDataChecker.Tests;

[TestFixture]
public class FileDbMatcherSqliteTests
{
    [Test]
    public async Task FindNonMatchingLineNumbersFromContentAsync_ReturnsExpectedNonMatches()
    {
        CancellationToken ct = CancellationToken.None;

        // Arrange: create and seed in-memory sqlite DB via test factory
        SqliteTestCommandFactory factory = new SqliteTestCommandFactory();

        StringBuilder setupCmdSb = new StringBuilder();
        setupCmdSb.AppendLine("CREATE TABLE TestBooks (");
        setupCmdSb.AppendLine("  Id INTEGER PRIMARY KEY,");
        setupCmdSb.AppendLine("  Title TEXT,");
        setupCmdSb.AppendLine("  Author TEXT,");
        setupCmdSb.AppendLine("  Publisher TEXT,");
        setupCmdSb.AppendLine("  PublicationYear INTEGER");
        setupCmdSb.AppendLine(");");
        setupCmdSb.AppendLine("INSERT INTO TestBooks (Title,Author,Publisher,PublicationYear) VALUES ('Match Title','Match Author','Match Publisher',2020);");
        setupCmdSb.AppendLine("INSERT INTO TestBooks (Title,Author,Publisher,PublicationYear) VALUES ('No Match Title','Other Author','Other Publisher',2019);");

        string setupCommandText = setupCmdSb.ToString();
        factory.SetupDatabase(setupCommandText);

        FileDbMatcher matcher = new FileDbMatcher(factory, new CsvHelperFileReaderService());

        // Build CSV content: header is line 1, two data rows (line 2 and 3)
        StringBuilder csvSb = new StringBuilder();
        csvSb.AppendLine("Title,Author,Publisher,Publication Year");
        csvSb.AppendLine("\"Match Title\",\"Match Author\",\"Match Publisher\",2020");
        csvSb.AppendLine("\"Not a Match Title\",\"Other Author\",\"Other Publisher\",2019");
        string csvContent = csvSb.ToString();

        string commandText = "SELECT COUNT(*) FROM TestBooks b WHERE b.Title = @Title AND b.Author = @Author AND b.Publisher = @Publisher AND b.PublicationYear = @PublicationYear";

        // Build QueryConfig object that targets the TestBooks table and maps parameters to CSV columns
        QueryConfig queryConfig = new QueryConfig
        {
            Name = "TestBooksCompare",
            CommandText = commandText,
            ConnectionString = null,
            Parameters = new List<ParameterConfig>
            {
                new() { Name = "@Title", DbType = "nvarchar", Size = -1, SourceColumn = "Title" },
                new() { Name = "@Author", DbType = "nvarchar", Size = -1, SourceColumn = "Author" },
                new() { Name = "@Publisher", DbType = "nvarchar", Size = -1, SourceColumn = "Publisher" },
                new() { Name = "@PublicationYear", DbType = "int", Size = null, SourceColumn = "Publication Year" }
            }
        };

        // Act
        List<int> nonMatches = await matcher.FindNonMatchingLineNumbersFromContentAsync(csvContent, queryConfig, ct).ConfigureAwait(false);

        // Assert - header is 1, first data row (line 2) matches, second data row (line 3) does not
        List<int> expected = new List<int> { 3 };
        Assert.That(nonMatches, Is.EqualTo(expected).AsCollection);

        factory.Dispose();
    }

    [Test]
    public async Task FindNonMatchingLineNumbersFromContentAsync_ReturnsExpectedNonMatches_90sUkPop_CSV()
    {
        CancellationToken ct = CancellationToken.None;

        // Arrange: create in-memory sqlite DB via test factory and create a TestSongs table with one row
        SqliteTestCommandFactory factory = new SqliteTestCommandFactory();
        using (DbCommand cmd = factory.CreateConnection(string.Empty).CreateCommand())
        {
            cmd.CommandText = @"
            CREATE TABLE TestSongs (
              Id INTEGER PRIMARY KEY,
              Title TEXT,
              Artist TEXT,
              ReleaseYear INTEGER
            );
            INSERT INTO TestSongs (Title,Artist,ReleaseYear) VALUES ('Wannabe','Spice Girls',1996);
        ";
            cmd.ExecuteNonQuery();
        }

        string commandText = "SELECT COUNT(*) FROM TestSongs s WHERE s.Title = @Title AND s.Artist = @Artist AND s.ReleaseYear = @Year";
        FileDbMatcher matcher = new FileDbMatcher(factory, new CsvHelperFileReaderService());

        // Build CSV content: header is line 1, two data rows (line 2 and 3)
        StringBuilder csvSb = new StringBuilder();
        csvSb.AppendLine("Title,Artist,Release Year");
        csvSb.AppendLine("\"Wannabe\",\"Spice Girls\",1996");            // should match DB row
        csvSb.AppendLine("\"Linger\",\"The Cranberries\",1990");         // not in DB -> expected non-match
        string csvContent = csvSb.ToString();

        // Build QueryConfig object mapping parameters to CSV columns
        QueryConfig queryConfig = new QueryConfig
        {
            Name = "TestSongsCompare90s",
            CommandText = commandText,
            ConnectionString = null,
            Parameters = new List<ParameterConfig>
        {
            new ParameterConfig { Name = "@Title", DbType = "nvarchar", Size = -1, SourceColumn = "Title" },
            new ParameterConfig { Name = "@Artist", DbType = "nvarchar", Size = -1, SourceColumn = "Artist" },
            new ParameterConfig { Name = "@Year", DbType = "int", Size = null, SourceColumn = "Release Year" }
        }
        };

        // Act
        List<int> nonMatches = await matcher.FindNonMatchingLineNumbersFromContentAsync(csvContent, queryConfig, ct).ConfigureAwait(false);

        // Assert - header is 1, first data row (line 2) matches, second data row (line 3) does not
        List<int> expected = new List<int> { 3 };
        Assert.That(nonMatches, Is.EqualTo(expected).AsCollection);

        factory.Dispose();
    }

    [Test]
    public async Task FindNonMatchingLineNumbersFromContentAsync_ReturnsExpectedNonMatches_90sUkPop_FixedFile()
    {
        CancellationToken ct = CancellationToken.None;

        // Arrange: create in-memory sqlite DB via test factory and create a TestSongs table with one row
        SqliteTestCommandFactory factory = new SqliteTestCommandFactory();
        using (DbCommand cmd = factory.CreateConnection(string.Empty).CreateCommand())
        {
            cmd.CommandText = @"
            CREATE TABLE TestSongs (
              Id INTEGER PRIMARY KEY,
              Title TEXT,
              Artist TEXT,
              ReleaseYear INTEGER
            );
            INSERT INTO TestSongs (Title,Artist,ReleaseYear) VALUES ('Wannabe','Spice Girls',1996);
        ";
            cmd.ExecuteNonQuery();
        }

        string commandText = "SELECT COUNT(*) FROM TestSongs s WHERE s.Title = @Title AND s.Artist = @Artist AND s.ReleaseYear = @Year";
        FileDbMatcher matcher = new FileDbMatcher(factory, new FixedWidthFileReaderService());

        // Build CSV content: header is line 1, two data rows (line 2 and 3)
        StringBuilder csvSb = new StringBuilder();
        csvSb.AppendLine(    "Wannabe   Spice Girls     1996");            // should match DB row
        csvSb.AppendLine(    "Linger    The Cranberries 1990");         // not in DB -> expected non-match
        string fileContent = csvSb.ToString();

        // Build QueryConfig object mapping parameters to CSV columns
        QueryConfig queryConfig = new QueryConfig
        {
            Name = "TestSongsCompare90s",
            CommandText = commandText,
            ConnectionString = null,
            Parameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "@Title", DbType = "nvarchar", Size = -1, SourceColumn = "Title" },
                new ParameterConfig { Name = "@Artist", DbType = "nvarchar", Size = -1, SourceColumn = "Artist" },
                new ParameterConfig { Name = "@Year", DbType = "int", Size = null, SourceColumn = "Release Year" }
            },
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "Title", StartColumn = 0, EndColumn = 10 },
                new ColumnConfig { Name = "Artist", StartColumn = 11, EndColumn = 26 },
                new ColumnConfig { Name = "Release Year", StartColumn = 27, EndColumn = 30 }
            }
        };

        // Act
        List<int> nonMatches = await matcher.FindNonMatchingLineNumbersFromContentAsync(fileContent, queryConfig, ct).ConfigureAwait(false);

        // Assert - header is 1, first data row (line 2) matches, second data row (line 3) does not
        List<int> expected = new List<int> { 3 };
        Assert.That(nonMatches, Is.EqualTo(expected).AsCollection);

        factory.Dispose();
    }


    [Test]
    public async Task FindNonMatchingLineNumbersFromContentAsync_AllRecordsMatch_AbstractArt()
    {
        CancellationToken ct = CancellationToken.None;

        // Arrange: in-memory sqlite DB with abstract art records
        SqliteTestCommandFactory factory = new SqliteTestCommandFactory();
        using (DbCommand cmd = factory.CreateConnection(string.Empty).CreateCommand())
        {
            cmd.CommandText = @"
            CREATE TABLE TestArt (
              Id INTEGER PRIMARY KEY,
              Title TEXT,
              Artist TEXT,
              Year INTEGER
            );
            INSERT INTO TestArt (Title,Artist,Year) VALUES ('Composition VII','Wassily Kandinsky',1913);
            INSERT INTO TestArt (Title,Artist,Year) VALUES ('Black Square','Kazimir Malevich',1915);
        ";
            cmd.ExecuteNonQuery();
        }

        string commandText = "SELECT COUNT(*) FROM TestArt a WHERE a.Title = @Title AND a.Artist = @Artist AND a.Year = @Year";
        FileDbMatcher matcher = new FileDbMatcher(factory, new CsvHelperFileReaderService());

        // CSV with two rows that both exist in DB
        StringBuilder csvSb = new StringBuilder();
        csvSb.AppendLine("Title,Artist,Year");
        csvSb.AppendLine("\"Composition VII\",\"Wassily Kandinsky\",1913");
        csvSb.AppendLine("\"Black Square\",\"Kazimir Malevich\",1915");
        string csvContent = csvSb.ToString();

        QueryConfig queryConfig = new QueryConfig
        {
            Name = "TestArtAllMatch",
            CommandText = commandText,
            ConnectionString = null,
            Parameters = new List<ParameterConfig>
        {
            new ParameterConfig { Name = "@Title", DbType = "nvarchar", Size = -1, SourceColumn = "Title" },
            new ParameterConfig { Name = "@Artist", DbType = "nvarchar", Size = -1, SourceColumn = "Artist" },
            new ParameterConfig { Name = "@Year", DbType = "int", Size = null, SourceColumn = "Year" }
        }
        };

        // Act
        List<int> nonMatches = await matcher.FindNonMatchingLineNumbersFromContentAsync(csvContent, queryConfig, ct).ConfigureAwait(false);

        // Assert - all data rows match, so no non-matching line numbers
        Assert.That(nonMatches, Is.Empty);

        factory.Dispose();
    }
    [Test]
    public async Task FindNonMatchingLineNumbersFromContentAsync_EmptyCsvContent_ReturnsEmptyList()
    {
        // Arrange
        CancellationToken ct = CancellationToken.None;
        SqliteTestCommandFactory factory = new SqliteTestCommandFactory();
        FileDbMatcher matcher = new FileDbMatcher(factory, new CsvHelperFileReaderService());

        // Build a minimal valid QueryConfig that passes validation but references a parameter in CommandText
        QueryConfig queryConfig = new QueryConfig
        {
            Name = "EmptyCsvTest",
            CommandText = "SELECT 1 WHERE @Title=@Title",
            ConnectionString = null,
            Parameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "@Title", DbType = "nvarchar", Size = -1, SourceColumn = "" }
            }
        };

        string csvContent = string.Empty; // empty content should cause csv.ReadAsync() to return false and method to return empty list

        // Act
        List<int> nonMatches = await matcher.FindNonMatchingLineNumbersFromContentAsync(csvContent, queryConfig, ct).ConfigureAwait(false);

        // Assert
        Assert.That(nonMatches, Is.Empty);

        factory.Dispose();
    }

}