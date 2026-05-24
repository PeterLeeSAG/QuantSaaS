using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Services;

// ── Dashboard ─────────────────────────────────────────────────────────────────

public record DashboardSummaryDto(
    decimal TotalEquity,
    int ActiveInstances,
    int TodayTrades,
    decimal AvailableFunds,
    IReadOnlyList<decimal> EquityCurve,
    IReadOnlyList<string> EquityLabels,
    IReadOnlyList<InstanceSummaryDto> Instances);

// ── Instances ─────────────────────────────────────────────────────────────────

public record InstanceSummaryDto(
    Guid Id,
    string Symbol,
    AssetClass AssetClass,
    string Status,
    decimal Equity,
    DateTime? LastTickAt);

public record InstanceRowDto(
    Guid Id,
    string Symbol,
    AssetClass AssetClass,
    string BrokerType,
    string Status,
    decimal Equity,
    DateTime? LastTickAt);

public record InstanceDetailDto(
    Guid Id,
    string Symbol,
    AssetClass AssetClass,
    string BrokerType,
    string Status,
    DateTime CreatedAt,
    DateTime? LastTickAt,
    decimal TotalEquity,
    decimal AvailableFunds,
    decimal LongTermHoldingsQty,
    decimal ActivePositionQty,
    decimal SealedQty,
    decimal PendingSettlementAmount,
    IReadOnlyList<decimal> EquityCurve,
    IReadOnlyList<string> EquityLabels,
    IReadOnlyList<TradeRowDto> RecentTrades,
    IReadOnlyList<PendingSettlementDto> PendingSettlements);

// ── Trades ────────────────────────────────────────────────────────────────────

public record TradeRowDto(
    string Symbol,
    AssetClass AssetClass,
    string Action,
    decimal FilledQty,
    decimal FilledPrice,
    decimal Fee,
    string Status,
    DateTime FilledAt);

public record TradePageDto(
    IReadOnlyList<TradeRowDto> Trades,
    int Page,
    int TotalPages,
    int TotalCount);

// ── Pending settlements ───────────────────────────────────────────────────────

public record PendingSettlementDto(
    string ClientOrderId,
    decimal Amount,
    DateTime SettlementDateUtc,
    bool IsSettled);

// ── Lab / Evolution ───────────────────────────────────────────────────────────

public record EvolutionTaskDto(
    Guid Id,
    string Symbol,
    AssetClass AssetClass,
    string Status,
    int Progress,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record GeneChampionDto(
    Guid Id,
    string Symbol,
    AssetClass AssetClass,
    string Role,
    decimal ScoreTotal,
    decimal MaxDrawdown,
    DateTime EvolvedAt);

public record LabDto(
    IReadOnlyList<EvolutionTaskDto> Tasks,
    IReadOnlyList<GeneChampionDto> Champions);

// ── User / Settings ───────────────────────────────────────────────────────────

public record AuthUserDto(
    Guid   Id,
    string Email,
    string PasswordHash,
    string Role);

public record UserSettingsDto(
    string Email,
    string SubscriptionPlan);
