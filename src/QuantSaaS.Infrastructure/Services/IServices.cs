namespace QuantSaaS.Infrastructure.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct = default);
}

public interface IInstanceService
{
    Task<IReadOnlyList<InstanceRowDto>> GetListAsync(Guid userId, string? assetClassFilter, CancellationToken ct = default);
    Task<InstanceDetailDto?> GetDetailAsync(Guid instanceId, CancellationToken ct = default);
}

public interface ITradeService
{
    Task<TradePageDto> GetPagedAsync(Guid userId, string? symbol, string? action, int page, CancellationToken ct = default);
}

public interface IEvolutionService
{
    Task<LabDto> GetLabAsync(Guid userId, CancellationToken ct = default);
}

public interface IUserService
{
    Task<AuthUserDto?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<AuthUserDto> CreateUserAsync(string email, string passwordHash, string role = "user", CancellationToken ct = default);
    Task<UserSettingsDto?> GetSettingsAsync(Guid userId, CancellationToken ct = default);
}
