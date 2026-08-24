using System;
using System.Collections.Generic;

public class FixedWidthFileReaderService : IFileReaderService
{
    // Read header from provided ColumnConfig list; if columns is null/empty, return empty header
    public string[] ReadHeader(string content, List<ColumnConfig>? columns = null)
    {
        if (columns == null || columns.Count == 0)
            return Array.Empty<string>();

        string[] headers = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
            headers[i] = columns[i].Name ?? string.Empty;
        return headers;
    }

    // Read rows by slicing each line according to ColumnConfig StartColumn/EndColumn (1-based)
    public IEnumerable<IReadOnlyDictionary<string, string?>> ReadRows(string content, List<ColumnConfig>? columns = null)
    {
        if (columns == null || columns.Count == 0)
            yield break;

        using StringReader sr = new StringReader(content);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            Dictionary<string, string?> dict = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                string name = col.Name ?? string.Empty;
                int startIdx = Math.Max(0, col.StartColumn - 1);
                int endIdx = Math.Max(0, col.EndColumn - 1);
                if (startIdx >= line.Length)
                {
                    dict[name] = null;
                    continue;
                }

                int len = Math.Min(line.Length - startIdx, endIdx - startIdx + 1);
                if (len <= 0)
                {
                    dict[name] = null;
                    continue;
                }

                string raw = line.Substring(startIdx, len);
                dict[name] = raw.Trim();
            }

            yield return dict;
        }
    }
}
