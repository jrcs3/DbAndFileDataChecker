using System.Data;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
// Example usage:
// var nonMatches = await CsvDbMatcher.FindNonMatchingLineNumbersAsync("..\\DbAndFileDataChecker.Tests\\SampleData\\UpperMidwest.csv");
// Console.WriteLine(string.Join(",", nonMatches));

public static class CsvDbMatcher
{
    public static async Task<List<int>> FindNonMatchingLineNumbersAsync(string csvFilePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csvFilePath))
            throw new ArgumentNullException(nameof(csvFilePath));

        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException("CSV file not found", csvFilePath);

        var nonMatches = new List<int>();
        var connStr = await GetDefaultConnectionStringAsync().ConfigureAwait(false);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        const string sql = @"SELECT COUNT(*) FROM [dbo].[RomanceNovels] r
JOIN [dbo].[Authors] a ON r.[AuthorId] = a.[AuthorId]
JOIN [dbo].[Publishers] p ON r.[PublisherId] = p.[PublisherId]
WHERE r.Title = @Title
    AND a.AuthorName = @AuthorName
    AND p.PublisherName = @PublisherName
    AND r.PublicationYear = @PublicationYear";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, -1));
        cmd.Parameters.Add(new SqlParameter("@AuthorName", SqlDbType.NVarChar, -1));
        cmd.Parameters.Add(new SqlParameter("@PublisherName", SqlDbType.NVarChar, -1));
        cmd.Parameters.Add(new SqlParameter("@PublicationYear", SqlDbType.Int));

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            BadDataFound = null
        };

        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvReader(reader, config);

        // Read header (line 1)
        if (!await csv.ReadAsync().ConfigureAwait(false))
            return nonMatches; // empty file

        csv.ReadHeader();
        int fileLine = 1; // header is line 1

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            fileLine++; // current record's file line number
            ct.ThrowIfCancellationRequested();

            var title = csv.GetField("Title")?.Trim();
            var author = csv.GetField("Author")?.Trim();
            var publisher = csv.GetField("Publisher")?.Trim();
            var pubYearText = csv.GetField("Publication Year")?.Trim();

            if (!int.TryParse(pubYearText, out var pubYear))
            {
                nonMatches.Add(fileLine);
                continue;
            }

            cmd.Parameters["@Title"].Value = (object?)title ?? DBNull.Value;
            cmd.Parameters["@AuthorName"].Value = (object?)author ?? DBNull.Value;
            cmd.Parameters["@PublisherName"].Value = (object?)publisher ?? DBNull.Value;
            cmd.Parameters["@PublicationYear"].Value = pubYear;

            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var matchCount = result is null || result is DBNull ? 0 : Convert.ToInt32(result);

            if (matchCount <= 0)
                nonMatches.Add(fileLine);
        }

        return nonMatches;
    }

    private static async Task<string> GetDefaultConnectionStringAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "appsettings.json");
        if (File.Exists(path))
        {
            await using var fs = File.OpenRead(path);
            var doc = await JsonDocument.ParseAsync(fs).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                cs.TryGetProperty("DefaultConnection", out var def) &&
                def.ValueKind == JsonValueKind.String)
            {
                return def.GetString() ?? throw new InvalidOperationException("DefaultConnection is empty");
            }
        }

        // Fallback to LocalDB connection string if appsettings.json is missing
        return "Server=(LocalDB)\\MSSQLLocalDB;Database=DbAndFileDataChecker;Trusted_Connection=True;TrustServerCertificate=True";
    }
}
