using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantSaaS.Infrastructure.Data;

public class UserEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(256)] public string Email { get; set; } = null!;
    [Required] public string PasswordHash { get; set; } = null!;
    [MaxLength(50)] public string Role { get; set; } = "user";
    [MaxLength(50)] public string Plan { get; set; } = "free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StrategyTemplateEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string StrategyId { get; set; } = null!;
    [Required, MaxLength(200)] public string Name { get; set; } = null!;
    [MaxLength(20)] public string Version { get; set; } = "1.0.0";
    public bool IsSpot { get; set; } = true;
    [Column(TypeName = "jsonb")] public string ManifestJson { get; set; } = "{}";
}

public class StrategyInstanceEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TemplateId { get; set; }
    [Required, MaxLength(20)] public string Symbol { get; set; } = "BTCUSDT";
    [MaxLength(20)] public string Status { get; set; } = "STOPPED"; // RUNNING|STOPPED|ERROR
    [MaxLength(10)] public string AggregationPeriod { get; set; } = "1d";
    public decimal FundQuota { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
}

public class PortfolioStateEntity
{
    [Key] public Guid InstanceId { get; set; }
    public decimal UsdtBalance { get; set; }
    public decimal DeadBtc { get; set; }
    public decimal FloatBtc { get; set; }
    public decimal ColdSealedBtc { get; set; }
    public long LastProcessedBarTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class RuntimeStateEntity
{
    [Key] public Guid InstanceId { get; set; }
    [Column(TypeName = "jsonb")] public string StateJson { get; set; } = "{}";
    public long LastUpdatedBarTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SpotLotEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    [MaxLength(20)] public string LotType { get; set; } = "DEAD_STACK"; // DEAD_STACK|FLOATING|COLD_SEALED
    public decimal Amount { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsColdSealed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TradeRecordEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    [MaxLength(100)] public string ClientOrderId { get; set; } = null!;
    [MaxLength(10)] public string Action { get; set; } = null!; // BUY|SELL
    [MaxLength(10)] public string Engine { get; set; } = null!; // MACRO|MICRO
    [MaxLength(20)] public string Symbol { get; set; } = null!;
    public decimal FilledQty { get; set; }
    public decimal FilledPrice { get; set; }
    public decimal Fee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SpotExecutionEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    [MaxLength(100)] public string ClientOrderId { get; set; } = null!;
    [MaxLength(20)] public string Status { get; set; } = "pending"; // pending|filled|failed
    [MaxLength(20)] public string LotType { get; set; } = "FLOATING";
    [Column(TypeName = "jsonb")] public string CommandJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FilledAt { get; set; }
}

public class AuditLogEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? InstanceId { get; set; }
    [MaxLength(50)] public string EventType { get; set; } = null!;
    [Column(TypeName = "jsonb")] public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GeneRecordEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public string StrategyId { get; set; } = null!;
    [MaxLength(20)] public string Role { get; set; } = "challenger"; // challenger|champion|retired
    [Column(TypeName = "jsonb")] public string ParamPackJson { get; set; } = "{}";
    public double ScoreTotal { get; set; }
    public decimal MaxDrawdown { get; set; }
    [Column(TypeName = "jsonb")] public string WindowScoresJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PromotedAt { get; set; }
}

public class EvolutionTaskEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(20)] public string Status { get; set; } = "pending"; // pending|running|completed|failed
    public int PopSize { get; set; } = 300;
    public int MaxGenerations { get; set; } = 25;
    public int Progress { get; set; }
    [Column(TypeName = "jsonb")] public string ConfigJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class KLineEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(20)] public string Symbol { get; set; } = null!;
    [Required, MaxLength(10)] public string Interval { get; set; } = null!;
    public long OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}
