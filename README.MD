# DbAndFileDataChecker

Lightweight utility to compare input file records against a database query and report which file records do not match any database row. 
It now supports CSV and Fixed With Files.

## Purpose of this project
I need to determine if a give input file has been imported into a database. This is a simple POC of that could do that.
I made a few simplifications for this experiment:
- Only CSV and Fixed Width files are supported (no Excel, JSON, etc.) 
- In the import process, all records would be inserted. 
  In the actual case, we will need to add some sort of filtering so the lines that are never imported don't create false failures.
- There has been some token dependancy injection, but more for convenience than testability or extensibility. In the actual case, the file types will be more varied and the code will need to be more extensible. (All the Database are SQL Server.)
- I use ADO.NET because it is easier to use when the database scheama is not known at compile time. I don't want to build lots of models for each table even in the actual case.

## What this does
This prototype is simplier than the actual use case, but it demonstrates the core functionality:
- Reads CSV content and a JSON `QueryConfig` describing a parameterized SQL query.
- For each CSV data row, runs the configured query with parameters mapped from CSV columns.
- Returns a list of CSV line numbers that did not match any row in the database (non-matches).

## Key techniques utilized
- CSV parsing: `CsvHelper` for robust header-aware CSV reading and streaming of large files.
- Parameterized queries & DB access: uses ADO.NET abstractions and `DbConnection`/`DbCommand` via an injected `IDbCommandFactory`.
- Provider-agnostic parameter handling: builds `DbParameter` instances from `QueryConfig` types to support different DB providers (SQLite, SQL Server, etc.).
- In-memory DB tests: tests use an in-memory SQLite instance for fast, isolated unit tests.
- Defensive validation: configuration and CSV headers are validated up-front (required parameters, missing columns, duplicate names).
  Since Copilot was utilized to generate the initial code, it may not be defensive enough for production use.
- Test automation: NUnit tests exercise happy-path and one edge case (time constraint).

## Why
- Ensures CSV data aligns with authoritative DB data before imports, reporting, or synchronization.
  Will need to be expanded for other file types.

## Getting started
Prerequisites:
- .NET 10 SDK
- `dotnet` CLI or Visual Studio 2026

Build and run tests:
- Build: `dotnet build`
- Run tests: `dotnet test`

Project layout (important files):
- `FileDbMatcher.cs` — core logic, public API `FindNonMatchingLineNumbersAsync` / `FindNonMatchingLineNumbersFromContentAsync`.
- `QueryConfig` / `ParameterConfig` — JSON-driven configuration used to map CSV columns to query parameters.
- `DbAndFileDataChecker.Tests` — NUnit tests that exercise the matcher; includes SQLite in-memory setup for integration-style tests.

Readers
- Use `--reader` command line switch
  - `csv` - CSV file reader (default). Don't need populate `Columns` array in config file.
  - `fixed` - Fixed Width file reader. Use the `Columns` array in the config file to define column positions and types.

## Config Files
The `--config` on the command points the tool to a JSON configuration file that describes the parameterized SQL query and how CSV columns map to query parameters. 
The program loads the JSON at startup, deserializes it into a `QueryConfig` object, validates required fields (for example, `CommandText` and at least one Parameter), and then uses that configuration when executing the query for each CSV row. 
Paths may be absolute or relative to the current working directory (I've only tested with absolute).

Config files are plain JSON with a structure like:
- `Name` (string): friendly name for the comparison run.
- `CommandText` (string): parameterized SQL containing placeholders (e.g., `@Title`).
- `ConnectionString` (optional): DB connection string; if omitted the system falls back to defaults.
- `Parameters` (array): items with Name (e.g., @Title), DbType (e.g., int, nvarchar), optional Size, and SourceColumn (CSV column name). The runner validates that each SourceColumn exists in the CSV header and that every parameter placeholder appears in CommandText. Supported DbType values include int, nvarchar, varchar, datetime, bit, float, decimal, and uniqueidentifier.
- `Columns` (array, optional): for Fixed Width files, defines the start and end positions of each column in the file. Each item has Name, StartColumn, EndColumn, and Type (string, int, datetime, etc.). The runner validates that all SourceColumns referenced in Parameters are defined in Columns.

Here is an example of a config file for CSV file:
```json
{
  "Name": "RomanceCompare",
  "CommandText": "SELECT COUNT(*) FROM [dbo].[RomanceNovels] WHERE r.Title = @Title",
  "ConnectionString": "Server=(LocalDB)\\MSSQLLocalDB;Database=DbAndFileDataChecker;Trusted_Connection=True;TrustServerCertificate=True",
  "Parameters": [
    {
      "Name": "@Title",
      "DbType": "NVarChar",
      "Size": -1,
      "SourceColumn": "Title"
    }
  ]
}
```

And for Fixed Width file:
```json
{
  "Name": "RomanceCompare",
  "CommandText": "SELECT COUNT(*) FROM [dbo].[RomanceNovels] WHERE r.Title = @Title",
  "ConnectionString": "Server=(LocalDB)\\MSSQLLocalDB;Database=DbAndFileDataChecker;Trusted_Connection=True;TrustServerCertificate=True",
  "Parameters": [
    {
      "Name": "@Title",
      "DbType": "NVarChar",
      "Size": -1,
      "SourceColumn": "Title"
    }
  ],
  "Columns": [
    { "Name": "Title", "StartColumn": 1, "EndColumn": 60, "Type": "string" }
  ]
}
```

## Usage (example)

``` cs
// Usage example
var factory = new MyDbCommandFactory(); // implements IDbCommandFactory
var fileReaderService = new CsvHelperFileReaderService(); // implements IFileReaderService
var matcher = new FileDbMatcher(factory, fileReaderService);

string csvPath = "data.csv";
string configJsonPath = "compare.json";
CancellationToken ct = CancellationToken.None;

List<int> nonMatches = await matcher.FindNonMatchingLineNumbersAsync(csvPath, configJsonPath, ct);
Console.WriteLine($"Non-matching CSV lines: {string.Join(',', nonMatches)}");
```
