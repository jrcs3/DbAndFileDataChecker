// Usage: dotnet run -- --file <path>    or  dotnet run -- -f <path>

async Task<int> MainAsync(string[] args)
{
    if (args is null)
    {
        Console.Error.WriteLine("No arguments provided.");
        PrintUsage();
        return 1;
    }

    string? filePath = null;
    string? configPath = null;
    string? readerOption = null; // e.g. "csv" (default) or "fixed"

    for (int i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a == "--file" || a == "-f")
        {
            if (i + 1 < args.Length)
            {
                filePath = args[i + 1];
                i++;
            }
        }
        else if (a == "--config" || a == "-c")
        {
            if (i + 1 < args.Length)
            {
                configPath = args[i + 1];
                i++;
            }
        }
        else if (a.StartsWith("--file="))
        {
            filePath = a.Substring("--file=".Length);
        }
        else if (a.StartsWith("--config="))
        {
            configPath = a.Substring("--config=".Length);
        }
        else if (a.StartsWith("-f="))
        {
            filePath = a.Substring("-f=".Length);
        }
        else if (a.StartsWith("-c="))
        {
            configPath = a.Substring("-c=".Length);
        }
        else if (a == "-r" || a == "--reader")
        {
            if (i + 1 < args.Length)
            {
                readerOption = args[i + 1];
                i++;
            }
        }
        else if (a.StartsWith("--reader="))
        {
            readerOption = a.Substring("--reader=".Length);
        }
        else if (a.StartsWith("-r="))
        {
            readerOption = a.Substring("-r=".Length);
        }
        else if (a == "-h" || a == "--help")
        {
            PrintUsage();
            return 0;
        }
    }

    if (string.IsNullOrWhiteSpace(filePath))
    {
        Console.Error.WriteLine("Missing required --file / -f argument.");
        PrintUsage();
        return 1;
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        Console.Error.WriteLine("Missing required --config / -c argument.");
        PrintUsage();
        return 1;
    }

    try
    {
        var factory = new SqlCommandFactory();

        // Choose file reader implementation
        IFileReaderService fileReaderService;
        if (!string.IsNullOrWhiteSpace(readerOption) && readerOption.Equals("fixed", StringComparison.OrdinalIgnoreCase))
        {
            fileReaderService = new FixedWidthFileReaderService();
        }
        else
        {
            fileReaderService = new CsvHelperFileReaderService();
        }

        var matcher = new CsvDbMatcher(factory, fileReaderService);
        var nonMatches = await matcher.FindNonMatchingLineNumbersAsync(filePath, configPath, CancellationToken.None).ConfigureAwait(false);
        if (nonMatches == null || nonMatches.Count == 0)
        {
            Console.WriteLine("All rows matched the database query.");
        }
        else
        {
            Console.WriteLine($"Non-matching line count: {nonMatches.Count}");
            Console.WriteLine(string.Join(',', nonMatches.Select(n => n.ToString())));
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 2;
    }
}

static void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run -- --file <path>    or    dotnet run -- -f <path>");
    Console.WriteLine("Options:");
    Console.WriteLine("  -f, --file <path>    Path to CSV file to check");
    Console.WriteLine("  -c, --config <path>  Path to JSON query config file");
    Console.WriteLine("  -r, --reader <csv|fixed>  Choose file reader implementation (default: csv)");
    Console.WriteLine("  -h, --help           Show this help");
}

return await MainAsync(args);
