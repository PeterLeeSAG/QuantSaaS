using Dapper;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class MsSqlUserService : IUserService
{
    private readonly DbConnectionFactory _db;

    public MsSqlUserService(DbConnectionFactory db) => _db = db;

    public async Task<UserSettingsDto?> GetSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT email, subscription_plan FROM dbo.users WHERE id = @Id",
            new { Id = userId });

        if (row is null) return null;

        return new UserSettingsDto((string)row.email, (string)row.subscription_plan);
    }
}
