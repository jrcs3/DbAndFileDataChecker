using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

// Example usage:
// var nonMatches = await CsvDbMatcher.FindNonMatchingLineNumbersAsync("..\\DbAndFileDataChecker.Tests\\SampleData\\UpperMidwest.csv", "..\\DbAndFileDataChecker.Tests\\RomanceCompare.json");
// Console.WriteLine(string.Join(",", nonMatches));

public class CsvDbMatcher
{
    private readonly IDbCommandFactory _factory;

    public CsvDbMatcher(IDbCommandFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
    /// <summary>
    /// Read CSV and JSON config files from disk and run the configured query per row.
    /// </summary>
    public async Task<List<int>> FindNonMatchingLineNumbersAsync(string csvFilePath, string configJsonPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csvFilePath))
            throw new ArgumentNullException(nameof(csvFilePath));

        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException("CSV file not found", csvFilePath);

        if (string.IsNullOrWhiteSpace(configJsonPath))
            throw new ArgumentNullException(nameof(configJsonPath));

        if (!File.Exists(configJsonPath))
            throw new FileNotFoundException("Query configuration JSON file not found", configJsonPath);

        string csvText = await File.ReadAllTextAsync(csvFilePath).ConfigureAwait(false);
        string? configText = await File.ReadAllTextAsync(configJsonPath).ConfigureAwait(false);

        if (configText is null) throw new ArgumentNullException(nameof(configText));

        QueryConfig? queryConfig = JsonSerializer.Deserialize<QueryConfig>(configText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (queryConfig is null || string.IsNullOrWhiteSpace(queryConfig.CommandText))
            throw new InvalidOperationException("Invalid query configuration content: missing CommandText.");


        return await FindNonMatchingLineNumbersFromContentAsync(csvText, queryConfig, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Core logic extracted to operate on loaded CSV and JSON content. This is public so tests can call it directly.
    /// </summary>
    public async Task<List<int>> FindNonMatchingLineNumbersFromContentAsync(string csvContent, QueryConfig queryConfig, CancellationToken ct = default)
    {
        if (queryConfig.Parameters == null || string.IsNullOrWhiteSpace(queryConfig.Name))
            throw new ArgumentNullException(nameof(queryConfig), "Invalid QueryConfig");
        Console.WriteLine($"Processing '{queryConfig.Name}'...");

        if (csvContent is null) throw new ArgumentNullException(nameof(csvContent));

        List<int> nonMatches = new List<int>();

        // Basic JSON validation
        if (queryConfig.Parameters == null || queryConfig.Parameters.Count == 0)
            throw new InvalidOperationException("Query configuration must define at least one parameter in 'Parameters'.");

        if (queryConfig.CommandText is null || !queryConfig.CommandText.Contains("@"))
            throw new InvalidOperationException("Query configuration must define a CommandText with at least one parameter placeholder (e.g. '@Title').");

        // Validate parameter names and types and duplicates
        HashSet<string> supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "int", "nvarchar", "varchar", "datetime", "bit", "float", "decimal", "uniqueidentifier"
        };

        string?[] duplicateNames = queryConfig.Parameters.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
            throw new InvalidOperationException($"Duplicate parameter names found in configuration: {string.Join(',', duplicateNames)}");

        foreach (ParameterConfig p in queryConfig.Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException("Each parameter must have a non-empty 'Name' field (e.g. '@Title').");

            if (!p.Name.StartsWith("@"))
                throw new InvalidOperationException($"Parameter name '{p.Name}' must start with '@'.");

            if (!string.IsNullOrWhiteSpace(p.DbType) && !supportedTypes.Contains(p.DbType))
                throw new InvalidOperationException($"Unsupported DbType '{p.DbType}' for parameter '{p.Name}'. Supported types: {string.Join(',', supportedTypes)}");
        }

        // Ensure parameter placeholders appear in the command text
        string?[] missingInCommand = queryConfig.Parameters.Where(p => !queryConfig.CommandText.Contains(p.Name!, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToArray();
        if (missingInCommand.Length > 0)
            throw new InvalidOperationException($"The following parameters are not referenced in CommandText: {string.Join(',', missingInCommand)}");

        CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            BadDataFound = null
        };

        using StringReader reader = new StringReader(csvContent);
        using CsvReader csv = new CsvReader(reader, csvConfig);

        // Read header (line 1)
        if (!await csv.ReadAsync().ConfigureAwait(false))
            return nonMatches; // empty file

        csv.ReadHeader();
        string[] header = csv.HeaderRecord ?? Array.Empty<string>();

        // Validate that all SourceColumn values exist in CSV header
        string[] missingSourceColumns = queryConfig.Parameters
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
        string connStr = !string.IsNullOrWhiteSpace(queryConfig.ConnectionString)
            ? queryConfig.ConnectionString
            : await GetDefaultConnectionStringAsync().ConfigureAwait(false);

        using DbConnection conn = _factory.CreateConnection(connStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using DbCommand cmd = conn.CreateCommand();
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

            object? result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            int matchCount = result is null || result is DBNull ? 0 : Convert.ToInt32(result);

            if (matchCount <= 0)
                nonMatches.Add(fileLine);
        }

        return nonMatches;
    }

    private static bool SetParmameters(QueryConfig queryConfig, List<int> nonMatches, CsvReader csv, DbCommand cmd, int fileLine)
    {
        if (queryConfig.Parameters == null)
            throw new InvalidOperationException("SetParmameters: QueryConfig.Parameters is null");

        bool skipRow = false;
        foreach (ParameterConfig p in queryConfig.Parameters)
        {
            string paramName = p.Name!;
            string sourceCol = p.SourceColumn ?? string.Empty;
            string? raw = csv.TryGetField(sourceCol, out string? val) ? val?.Trim() : null;

            string dbType = (p.DbType ?? "").Trim();
            if (dbType.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(raw, out int parsedInt))
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

    private static void BuildParamaters(QueryConfig queryConfig, DbCommand cmd)
    {
        if (queryConfig.Parameters == null)
            throw new InvalidOperationException("BuildParamaters: QueryConfig.Parameters is null");

        // Build parameters from configuration using provider-agnostic DbParameter
        foreach (ParameterConfig p in queryConfig.Parameters)
        {
            string paramName = p.Name!;
            DbParameter dbParam = cmd.CreateParameter();
            dbParam.ParameterName = paramName;
            string dbType = (p.DbType ?? "").Trim();
            switch (dbType.ToLowerInvariant())
            {
                case "int":
                    dbParam.DbType = System.Data.DbType.Int32;
                    break;
                case "nvarchar":
                case "varchar":
                    dbParam.DbType = System.Data.DbType.String;
                    int size = p.Size ?? -1;
                    if (size > 0) dbParam.Size = size;
                    break;
                case "datetime":
                    dbParam.DbType = System.Data.DbType.DateTime;
                    break;
                case "bit":
                    dbParam.DbType = System.Data.DbType.Boolean;
                    break;
                case "float":
                    dbParam.DbType = System.Data.DbType.Double;
                    break;
                case "decimal":
                    dbParam.DbType = System.Data.DbType.Decimal;
                    break;
                case "uniqueidentifier":
                    dbParam.DbType = System.Data.DbType.Guid;
                    break;
                default:
                    dbParam.DbType = System.Data.DbType.String;
                    int defSize = p.Size ?? -1;
                    if (defSize > 0) dbParam.Size = defSize;
                    break;
            }

            // Add the parameter now; value will be set per-row
            cmd.Parameters.Add(dbParam);
        }
    }

    private static async Task<string> GetDefaultConnectionStringAsync()
    {
        string baseDir = AppContext.BaseDirectory;
        string path = Path.Combine(baseDir, "appsettings.json");
        if (File.Exists(path))
        {
            await using FileStream fs = File.OpenRead(path);
            JsonDocument doc = await JsonDocument.ParseAsync(fs).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out JsonElement cs) &&
                cs.TryGetProperty("DefaultConnection", out JsonElement def) &&
                def.ValueKind == JsonValueKind.String)
            {
                return def.GetString() ?? throw new InvalidOperationException("DefaultConnection is empty");
            }
        }

        // Fallback to LocalDB connection string if appsettings.json is missing
        return "Server=(LocalDB)\\MSSQLLocalDB;Database=DbAndFileDataChecker;Trusted_Connection=True;TrustServerCertificate=True";
    }

}
