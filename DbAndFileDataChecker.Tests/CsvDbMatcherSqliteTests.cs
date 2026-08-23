using System.Text;

namespace DbAndFileDataChecker.Tests;

[TestFixture]
public class CsvDbMatcherSqliteTests
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

        CsvDbMatcher matcher = new CsvDbMatcher(factory);

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
}