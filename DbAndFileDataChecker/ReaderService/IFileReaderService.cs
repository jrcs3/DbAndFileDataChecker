using System.Collections.Generic;

public interface IFileReaderService
{
    // Read header columns from content. Columns parameter reserved for future use.
    string[] ReadHeader(string content, List<ColumnConfig>? columns = null);

    // Read rows as dictionaries mapping header name -> value (trimmed). Columns parameter reserved for future use.
    IEnumerable<IReadOnlyDictionary<string, string?>> ReadRows(string content, List<ColumnConfig>? columns = null);
}
