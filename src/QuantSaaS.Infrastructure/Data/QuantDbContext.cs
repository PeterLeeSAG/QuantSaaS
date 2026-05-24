using Microsoft.EntityFrameworkCore;

namespace QuantSaaS.Infrastructure.Data;

public class QuantDbContext : DbContext
{
    public QuantDbContext(DbContextOptions<QuantDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<StrategyTemplateEntity> StrategyTemplates => Set<StrategyTemplateEntity>();
    public DbSet<StrategyInstanceEntity> StrategyInstances => Set<StrategyInstanceEntity>();
    public DbSet<PortfolioStateEntity> PortfolioStates => Set<PortfolioStateEntity>();
    public DbSet<RuntimeStateEntity> RuntimeStates => Set<RuntimeStateEntity>();
    public DbSet<SpotLotEntity> SpotLots => Set<SpotLotEntity>();
    public DbSet<TradeRecordEntity> TradeRecords => Set<TradeRecordEntity>();
    public DbSet<SpotExecutionEntity> SpotExecutions => Set<SpotExecutionEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<GeneRecordEntity> GeneRecords => Set<GeneRecordEntity>();
    public DbSet<EvolutionTaskEntity> EvolutionTasks => Set<EvolutionTaskEntity>();
    public DbSet<KLineEntity> KLines => Set<KLineEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KLineEntity>()
            .HasIndex(k => new { k.Symbol, k.Interval, k.OpenTime })
            .IsUnique();
        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Email).IsUnique();
    }
}
