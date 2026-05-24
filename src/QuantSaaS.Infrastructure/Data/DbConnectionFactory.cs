using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace QuantSaaS.Infrastructure.Data;

/// <summary>
/// Provider-agnostic connection factory supporting PostgreSQL and SQL Server (MSSQL).
/// Auto-detects the target database from the connection string: if it contains
/// "Server=" or ("Data Source=" and "Initial Catalog=") it is treated as SQL Server;
/// everything else is treated as PostgreSQL.
/// Register as Singleton; each provider manages its own connection pool internally.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly bool _isMsSql;
    private readonly NpgsqlDataSource? _pgDataSource;
    private readonly string? _msSqlConnectionString;

    public DbConnectionFactory(string connectionString)
    {
        _isMsSql = IsSqlServer(connectionString);
        if (_isMsSql)
            _msSqlConnectionString = connectionString;
        else
            _pgDataSource = NpgsqlDataSource.Create(connectionString);
    }

    /// <summary>Opens and returns a ready-to-use connection.</summary>
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        if (_isMsSql)
        {
            var conn = new SqlConnection(_msSqlConnectionString);
            await conn.OpenAsync(ct);
            return conn;
        }

        return await _pgDataSource!.OpenConnectionAsync(ct);
    }

    private static bool IsSqlServer(string cs) =>
        cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        (cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
         cs.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase));
}
