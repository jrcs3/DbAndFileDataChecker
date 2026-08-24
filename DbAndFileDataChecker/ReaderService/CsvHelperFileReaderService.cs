using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;

public class CsvHelperFileReaderService : IFileReaderService
{
    private readonly CsvConfiguration _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        IgnoreBlankLines = true,
        BadDataFound = null
    };

    public string[] ReadHeader(string content, List<ColumnConfig>? columns = null)
    {
        using StringReader sr = new StringReader(content);
        using CsvReader csv = new CsvReader(sr, _csvConfig);
        if (!csv.Read()) return Array.Empty<string>();
        csv.ReadHeader();
        return csv.HeaderRecord ?? Array.Empty<string>();
    }

    public IEnumerable<IReadOnlyDictionary<string, string?>> ReadRows(string content, List<ColumnConfig>? columns = null)
    {
        using StringReader sr = new StringReader(content);
        using CsvReader csv = new CsvReader(sr, _csvConfig);

        if (!csv.Read()) yield break;
        csv.ReadHeader();
        string[] headers = csv.HeaderRecord ?? Array.Empty<string>();

        while (csv.Read())
        {
            Dictionary<string, string?> dict = new Dictionary<string, string?>();
            foreach (string h in headers)
            {
                string key = h ?? string.Empty;
                string? val = csv.TryGetField(key, out string? v) ? v?.Trim() : null;
                dict[key] = val;
            }

            yield return dict;
        }
    }
}
