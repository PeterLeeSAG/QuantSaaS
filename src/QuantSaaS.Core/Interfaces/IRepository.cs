using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

public interface IRepository
{
    Task<PortfolioState?> GetPortfolioStateAsync(Guid instanceId, CancellationToken ct = default);
    Task UpdatePortfolioStateAsync(Guid instanceId, PortfolioState state, CancellationToken ct = default);
    Task<StrategyRuntimeState> GetRuntimeStateAsync(Guid instanceId, CancellationToken ct = default);
    Task SaveRuntimeStateAsync(Guid instanceId, StrategyRuntimeState state, CancellationToken ct = default);
    Task<KLineBar?> GetLatestCompletedBarAsync(string symbol, string interval, CancellationToken ct = default);
    Task<KLineBar[]> GetBarsAsync(string symbol, string interval, long fromMs, long toMs, CancellationToken ct = default);
    Task WriteAuditLogAsync(Guid instanceId, string eventType, object payload, CancellationToken ct = default);
    Task MarkInstanceErrorAsync(Guid instanceId, string message, CancellationToken ct = default);
}

public record KLineBar
{
    public long OpenTime { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
}
