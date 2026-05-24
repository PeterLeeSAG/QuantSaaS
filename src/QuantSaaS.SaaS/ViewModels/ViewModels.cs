using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Models;

namespace QuantSaaS.SaaS.ViewModels;

public class DashboardViewModel
{
    public decimal TotalEquity { get; set; }
    public int ActiveInstances { get; set; }
    public int TodayTrades { get; set; }
    public decimal AvailableFunds { get; set; }
    public List<decimal> EquityCurve { get; set; } = [];
    public List<string> EquityLabels { get; set; } = [];
    public List<InstanceSummary> Instances { get; set; } = [];
}

public class InstanceSummary
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Equity { get; set; }
    public DateTime? LastTickAt { get; set; }
}

public class InstanceListViewModel
{
    public List<InstanceRow> Instances { get; set; } = [];
    public string ActiveTab { get; set; } = "All";
}

public class InstanceRow
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string BrokerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Equity { get; set; }
    public DateTime? LastTickAt { get; set; }
}

public class InstanceDetailViewModel
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string BrokerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastTickAt { get; set; }

    // Portfolio
    public decimal TotalEquity { get; set; }
    public decimal AvailableFunds { get; set; }
    public decimal LongTermHoldingsQty { get; set; }
    public decimal ActivePositionQty { get; set; }
    public decimal SealedQty { get; set; }
    public decimal PendingSettlementAmount { get; set; }

    // Chart
    public List<decimal> EquityCurve { get; set; } = [];
    public List<string> EquityLabels { get; set; } = [];

    // Trades
    public List<TradeRow> RecentTrades { get; set; } = [];
    public List<PendingSettlementRow> PendingSettlements { get; set; } = [];
}

public class TradeRow
{
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Action { get; set; } = string.Empty;
    public decimal FilledQty { get; set; }
    public decimal FilledPrice { get; set; }
    public decimal Fee { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime FilledAt { get; set; }
}

public class PendingSettlementRow
{
    public string ClientOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime SettlementDateUtc { get; set; }
    public bool IsSettled { get; set; }
}

public class LabViewModel
{
    public List<EvolutionTaskRow> Tasks { get; set; } = [];
    public List<GeneRow> Champions { get; set; } = [];
}

public class EvolutionTaskRow
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class GeneRow
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal ScoreTotal { get; set; }
    public decimal MaxDrawdown { get; set; }
    public DateTime EvolvedAt { get; set; }
}

public class TradesViewModel
{
    public List<TradeRow> Trades { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? SymbolFilter { get; set; }
    public string? ActionFilter { get; set; }
}

public class SettingsViewModel
{
    public string Email { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = string.Empty;
    public List<BrokerStatus> Brokers { get; set; } = [];
    public bool AgentOnline { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
}

public class BrokerStatus
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Connected { get; set; }
    public string AccountId { get; set; } = string.Empty;
}
