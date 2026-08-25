using System.Data.Common;

public interface IDbCommandFactory
{
    /// <summary>
    /// Create a DbConnection for the provided connection string. Caller is responsible for opening the connection.
    /// </summary>
    DbConnection CreateConnection(string connectionString);
}
