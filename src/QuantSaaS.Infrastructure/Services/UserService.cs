using Dapper;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly DbConnectionFactory _db;

    public UserService(DbConnectionFactory db) => _db = db;

    public async Task<AuthUserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT id, email, password_hash, role FROM users WHERE email = @Email",
            new { Email = email });

        if (row is null) return null;

        return new AuthUserDto(
            (Guid)row.id,
            (string)row.email,
            (string)row.password_hash,
            (string)row.role);
    }

    public async Task<AuthUserDto> CreateUserAsync(string email, string passwordHash, string role = "user", CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        using var conn = await _db.OpenAsync(ct);

        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, password_hash, role, subscription_plan, created_at)
            VALUES (@Id, @Email, @PasswordHash, @Role, 'free', @CreatedAt)
            """,
            new { Id = id, Email = email, PasswordHash = passwordHash, Role = role, CreatedAt = DateTime.UtcNow });

        return new AuthUserDto(id, email, passwordHash, role);
    }

    public async Task<UserSettingsDto?> GetSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT email, subscription_plan FROM users WHERE id = @Id",
            new { Id = userId });

        if (row is null) return null;

        return new UserSettingsDto((string)row.email, (string)row.subscription_plan);
    }
}
