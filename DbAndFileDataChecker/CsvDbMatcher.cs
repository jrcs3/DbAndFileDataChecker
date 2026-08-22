using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;

// Example usage:
// var nonMatches = await CsvDbMatcher.FindNonMatchingLineNumbersAsync("..\\DbAndFileDataChecker.Tests\\SampleData\\UpperMidwest.csv", "..\\DbAndFileDataChecker.Tests\\RomanceCompare.json");
// Console.WriteLine(string.Join(",", nonMatches));

public static class CsvDbMatcher
{
    /// <summary>
    /// Read CSV and JSON config files from disk and run the configured query per row.
    /// </summary>
    public static async Task<List<int>> FindNonMatchingLineNumbersAsync(string csvFilePath, string configJsonPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csvFilePath))
            throw new ArgumentNullException(nameof(csvFilePath));

        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException("CSV file not found", csvFilePath);

        if (string.IsNullOrWhiteSpace(configJsonPath))
            throw new ArgumentNullException(nameof(configJsonPath));

        if (!File.Exists(configJsonPath))
            throw new FileNotFoundException("Query configuration JSON file not found", configJsonPath);

        var csvText = await File.ReadAllTextAsync(csvFilePath).ConfigureAwait(false);
        var configText = await File.ReadAllTextAsync(configJsonPath).ConfigureAwait(false);

        if (configText is null) throw new ArgumentNullException(nameof(configText));

        QueryConfig? queryConfig = JsonSerializer.Deserialize<QueryConfig>(configText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (queryConfig is null || string.IsNullOrWhiteSpace(queryConfig.CommandText))
            throw new InvalidOperationException("Invalid query configuration content: missing CommandText.");


        return await FindNonMatchingLineNumbersFromContentAsync(csvText, queryConfig, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Core logic extracted to operate on loaded CSV and JSON content. This is public so tests can call it directly.
    /// </summary>
    public static async Task<List<int>> FindNonMatchingLineNumbersFromContentAsync(string csvContent, QueryConfig queryConfig, CancellationToken ct = default)
    {
        if (csvContent is null) throw new ArgumentNullException(nameof(csvContent));

        var nonMatches = new List<int>();

        // Basic JSON validation
        if (queryConfig.Parameters == null || queryConfig.Parameters.Count == 0)
            throw new InvalidOperationException("Query configuration must define at least one parameter in 'Parameters'.");

        // Validate parameter names and types and duplicates
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "int", "nvarchar", "varchar", "datetime", "bit", "float", "decimal", "uniqueidentifier"
        };

        var duplicateNames = queryConfig.Parameters.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
            throw new InvalidOperationException($"Duplicate parameter names found in configuration: {string.Join(',', duplicateNames)}");

        foreach (var p in queryConfig.Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException("Each parameter must have a non-empty 'Name' field (e.g. '@Title').");

            if (!p.Name.StartsWith("@"))
                throw new InvalidOperationException($"Parameter name '{p.Name}' must start with '@'.");

            if (!string.IsNullOrWhiteSpace(p.DbType) && !supportedTypes.Contains(p.DbType))
                throw new InvalidOperationException($"Unsupported DbType '{p.DbType}' for parameter '{p.Name}'. Supported types: {string.Join(',', supportedTypes)}");
        }

        // Ensure parameter placeholders appear in the command text
        var missingInCommand = queryConfig.Parameters.Where(p => !queryConfig.CommandText.Contains(p.Name!, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToArray();
        if (missingInCommand.Length > 0)
            throw new InvalidOperationException($"The following parameters are not referenced in CommandText: {string.Join(',', missingInCommand)}");

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            BadDataFound = null
        };

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, csvConfig);

        // Read header (line 1)
        if (!await csv.ReadAsync().ConfigureAwait(false))
            return nonMatches; // empty file

        csv.ReadHeader();
        var header = csv.HeaderRecord ?? Array.Empty<string>();

        // Validate that all SourceColumn values exist in CSV header
        var missingSourceColumns = queryConfig.Parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.SourceColumn))
            .Select(p => p.SourceColumn!)
            .Where(sc => !header.Contains(sc))
            .Distinct()
            .ToArray();

        if (missingSourceColumns.Length > 0)
        {
            throw new InvalidOperationException($"The following SourceColumn names from configuration were not found in the CSV header: {string.Join(',', missingSourceColumns)}. CSV header columns: {string.Join(',', header)}");
        }

        // Now that validation passed, open DB connection
        var connStr = !string.IsNullOrWhiteSpace(queryConfig.ConnectionString)
            ? queryConfig.ConnectionString
            : await GetDefaultConnectionStringAsync().ConfigureAwait(false);

        await using SqlConnection conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = queryConfig.CommandText;

        BuildParamaters(queryConfig, cmd);

        int fileLine = 1; // header is line 1

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            fileLine++; // current record's file line number
            ct.ThrowIfCancellationRequested();

            // Set parameter values from configured SourceColumn mapping
            bool skipRow = SetParmameters(queryConfig, nonMatches, csv, cmd, fileLine);

            if (skipRow)
                continue;

            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var matchCount = result is null || result is DBNull ? 0 : Convert.ToInt32(result);

            if (matchCount <= 0)
                nonMatches.Add(fileLine);
        }

        return nonMatches;
    }

    private static bool SetParmameters(QueryConfig queryConfig, List<int> nonMatches, CsvReader csv, SqlCommand cmd, int fileLine)
    {
        var skipRow = false;
        foreach (var p in queryConfig.Parameters)
        {
            var paramName = p.Name!;
            var sourceCol = p.SourceColumn ?? string.Empty;
            var raw = csv.TryGetField(sourceCol, out string? val) ? val?.Trim() : null;

            var dbType = (p.DbType ?? "").Trim();
            if (dbType.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(raw, out var parsedInt))
                {
                    // Treat missing/non-integer as non-match
                    nonMatches.Add(fileLine);
                    skipRow = true;
                    break;
                }

                cmd.Parameters[paramName].Value = parsedInt;
            }
            else
            {
                cmd.Parameters[paramName].Value = (object?)raw ?? DBNull.Value;
            }
        }

        return skipRow;
    }

    private static void BuildParamaters(QueryConfig queryConfig, SqlCommand cmd)
    {
        // Build parameters from configuration
        foreach (var p in queryConfig.Parameters)
        {
            var paramName = p.Name!;
            SqlParameter sqlParam;
            var dbType = (p.DbType ?? "").Trim();
            switch (dbType.ToLowerInvariant())
            {
                case "int":
                    sqlParam = new SqlParameter(paramName, SqlDbType.Int);
                    break;
                case "nvarchar":
                    var size = p.Size ?? -1;
                    sqlParam = new SqlParameter(paramName, SqlDbType.NVarChar, size);
                    break;
                case "varchar":
                    var vsize = p.Size ?? -1;
                    sqlParam = new SqlParameter(paramName, SqlDbType.VarChar, vsize);
                    break;
                case "datetime":
                    sqlParam = new SqlParameter(paramName, SqlDbType.DateTime);
                    break;
                case "bit":
                    sqlParam = new SqlParameter(paramName, SqlDbType.Bit);
                    break;
                case "float":
                    sqlParam = new SqlParameter(paramName, SqlDbType.Float);
                    break;
                case "decimal":
                    sqlParam = new SqlParameter(paramName, SqlDbType.Decimal);
                    break;
                case "uniqueidentifier":
                    sqlParam = new SqlParameter(paramName, SqlDbType.UniqueIdentifier);
                    break;
                default:
                    var defSize = p.Size ?? -1;
                    sqlParam = new SqlParameter(paramName, SqlDbType.NVarChar, defSize);
                    break;
            }

            // Add the parameter now; value will be set per-row
            cmd.Parameters.Add(sqlParam);
        }
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
