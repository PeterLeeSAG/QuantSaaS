using System;
using System.Collections.Generic;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Models;

// ── GORM-style POCO entities ────────────────────────────────────────────────
// Iron Rule: Schema is defined here via C# class properties (Code-First / AutoMigrate).
// Never write SQL migration files.

/// <summary>Registered user with subscription plan.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = "free"; // free | starter | pro
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<StrategyInstance> Instances { get; set; } = [];
}

/// <summary>
/// Registered strategy template.
/// Each template defines the strategy code to run (StrategyId), the asset class,
/// and its Manifest JSON.
/// </summary>
public class StrategyTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StrategyId { get; set; } = string.Empty;     // e.g., "btc-spot-v1"
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public bool IsSpotOnly { get; set; }
    public string ManifestJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A user's active strategy instance.
/// Extended to support multi-asset: carries InstrumentSymbol, AssetClass, BrokerType.
/// </summary>
public class StrategyInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public StrategyTemplate Template { get; set; } = null!;

    // ── Instrument ─────────────────────────────────────────────────────────

    public string InstrumentSymbol { get; set; } = string.Empty;  // e.g., "BTC/USDT", "AAPL"
    public AssetClass AssetClass { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = "USD";
    public string BrokerType { get; set; } = string.Empty; // "okx" | "alpaca" | "ibkr"

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public string Status { get; set; } = "stopped";  // running | stopped | error
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTickAt { get; set; }

    // ── GA parameters ──────────────────────────────────────────────────────

    public string? ActiveParamPackJson { get; set; }  // champion's ParamPack

    // ── Relations ──────────────────────────────────────────────────────────

    public PortfolioSnapshot? Portfolio { get; set; }
    public List<TradeRecord> TradeRecords { get; set; } = [];
    public List<PendingSettlement> PendingSettlements { get; set; } = [];
}

/// <summary>Portfolio state snapshot for an instance.</summary>
public class PortfolioSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public StrategyInstance Instance { get; set; } = null!;

    public decimal CashBalance { get; set; }
    public decimal PendingSettlementAmount { get; set; }
    public decimal DeadStackQty { get; set; }
    public decimal FloatStackQty { get; set; }
    public decimal ColdSealedQty { get; set; }
    public decimal TotalEquity { get; set; }

    public long LastProcessedBarMs { get; set; }
    public string RuntimeStateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Individual position lot record.</summary>
public class SpotLot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }

    public string LotType { get; set; } = "FLOATING"; // DEAD_STACK | FLOATING | COLD_SEALED
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsColdSealed { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Trade record for audit and reporting.</summary>
public class TradeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public StrategyInstance Instance { get; set; } = null!;

    public string ClientOrderId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;   // BUY | SELL
    public string Engine { get; set; } = string.Empty;   // MACRO | MICRO
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }

    public decimal FilledQty { get; set; }
    public decimal FilledPrice { get; set; }
    public decimal Fee { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime FilledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Pending T+2 settlement item for equity accounts.</summary>
public class PendingSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public StrategyInstance Instance { get; set; } = null!;

    public string ClientOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime SettlementDateUtc { get; set; }
    public bool IsSettled { get; set; }
}

/// <summary>
/// Corporate action record for price adjustment and dividend accounting.
/// </summary>
public class CorporateActionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;   // Split | Dividend | Spinoff
    public DateTime ExDateUtc { get; set; }
    public decimal Amount { get; set; }
    public bool Applied { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>OHLCV bar storage – unified table for all asset classes.</summary>
public class BarRecord
{
    public long Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>Unix-ms timestamp of bar open. Combined with Symbol+Timeframe forms unique key.</summary>
    public long OpenTimeMs { get; set; }

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}

/// <summary>Tradable instrument registry.</summary>
public class InstrumentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = "USD";
    public decimal LotStep { get; set; } = 1m;
    public decimal LotMin { get; set; } = 1m;
    public decimal TickSize { get; set; } = 0.01m;
    public bool FractionAllowed { get; set; }
    public int SettlementDays { get; set; }
    public string CalendarType { get; set; } = "crypto"; // crypto | us_equity
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// GA genome record (challenger → champion → retired lifecycle).
/// </summary>
public class GeneRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public AssetClass AssetClass { get; set; }
    public string Symbol { get; set; } = string.Empty;

    public string Role { get; set; } = "challenger"; // challenger | champion | retired
    public string ParamPackJson { get; set; } = "{}";
    public decimal ScoreTotal { get; set; }
    public decimal MaxDrawdown { get; set; }

    public DateTime EvolvedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PromotedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
}

/// <summary>GA evolution task record.</summary>
public class EvolutionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }

    public string Status { get; set; } = "pending";  // pending | running | completed | failed
    public int Progress { get; set; }                 // 0–100
    public string ConfigJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Guid? ResultGeneRecordId { get; set; }
}

/// <summary>Audit log for all state transitions.</summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? InstanceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Daily NAV snapshot used to render equity curves.
/// A null InstanceId denotes an aggregate (user-level) snapshot.
/// </summary>
public class EquitySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? InstanceId { get; set; }   // null = user aggregate
    public DateTime DateUtc { get; set; }
    public decimal Equity { get; set; }
}
