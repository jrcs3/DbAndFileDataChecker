using Microsoft.Data.SqlClient;
using System.Data.Common;

public class SqlCommandFactory : IDbCommandFactory
{
    public DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
