using System.Data;
using Npgsql;

namespace QuantSaaS.Infrastructure.Data;

/// <summary>
/// Creates and opens a new PostgreSQL connection for each call.
/// Register as Singleton; the underlying NpgsqlDataSource manages the pool.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public DbConnectionFactory(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    /// <summary>Opens and returns a ready-to-use connection.</summary>
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        return await _dataSource.OpenConnectionAsync(ct);
    }
}
