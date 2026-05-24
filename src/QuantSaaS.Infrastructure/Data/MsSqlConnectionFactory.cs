using System.Data;
using Microsoft.Data.SqlClient;

namespace QuantSaaS.Infrastructure.Data;

/// <summary>
/// Opens a new SQL Server connection for each call (MSSQL 2022 Express).
/// Register as Singleton; SqlClient manages the underlying connection pool.
/// </summary>
public sealed class MsSqlConnectionFactory
{
    private readonly string _connectionString;

    public MsSqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Opens and returns a ready-to-use SQL Server connection.</summary>
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
