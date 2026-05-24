namespace QuantSaaS.SaaS.Models;

// ── System Status ──────────────────────────────
public record SystemStatusResponse(
    string EngineStatus,
    bool AgentConnected,
    string? UserEmail,
    long LastCheckedMs);

// ── Instances ──────────────────────────────────
public record InstanceSummary(
    Guid Id,
    string Symbol,
    string Status,
    string AggregationPeriod,
    decimal FundQuota,
    DateTime CreatedAt,
    DateTime? StartedAt,
    decimal? TotalEquity);

public record CreateInstanceRequest(
    string Symbol,
    string AggregationPeriod,
    decimal FundQuota);

// ── Dashboard ──────────────────────────────────
public record DashboardData(
    Guid InstanceId,
    decimal UsdtBalance,
    decimal LongTermHoldings,    // DeadBTC * price (user-facing)
    decimal ActivePosition,       // FloatBTC * price (user-facing)
    decimal SealedAssets,         // ColdSealedBTC * price
    decimal TotalEquity,
    string MarketState,
    long LastDecisionTime,
    int TotalTrades);

public record EquityPoint(long TimestampMs, decimal Equity);

// ── Evolution ──────────────────────────────────
public record EvolutionTaskSummary(
    Guid Id,
    string Status,
    int PopSize,
    int MaxGenerations,
    int Progress,
    double? BestScore,
    decimal? MaxDrawdown,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

public record GenomeSummary(
    Guid Id,
    string Role,
    double ScoreTotal,
    decimal MaxDrawdown,
    DateTime CreatedAt,
    DateTime? PromotedAt,
    List<WindowScoreDto> WindowScores);

public record WindowScoreDto(string Label, decimal Weight, decimal SliceScore, decimal Alpha, decimal MaxDrawdown);

public record TriggerEvolutionRequest(int PopSize, int MaxGenerations, string? SeedParamPackJson);

public record PromoteGenomeRequest(Guid GenomeId);
