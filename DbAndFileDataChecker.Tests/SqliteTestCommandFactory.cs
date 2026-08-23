using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace DbAndFileDataChecker.Tests;

// Test-only IDbCommandFactory implementation for SQLite in-memory
public class SqliteTestCommandFactory : IDbCommandFactory, IDisposable
{
    private readonly SqliteConnection _connection;

        public SqliteTestCommandFactory()
        {
            SQLitePCL.Batteries.Init();
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

    // IDbCommandFactory contract used by production code
    public DbConnection CreateConnection(string connectionString)
    {
        // Return the in-memory open connection regardless of the provided connection string
        return _connection;
    }

    // Setup method that creates schema and seeds data. CommandText is passed in for test visibility.
    public void SetupDatabase(string commandText)
    {
        DbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.ExecuteNonQuery();
        cmd.Dispose();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}