using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Services;

namespace QuantSaaS.Tests.Services;

public class InstanceManagerTests
{
    private static QuantDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<QuantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuantDbContext(opts);
    }

    private static (InstanceManager, QuantDbContext) CreateManager()
    {
        var db = CreateDb();
        // We'll re-use the same DbContext via a stub scope factory
        var services = new ServiceCollection();
        services.AddDbContext<QuantDbContext>(opts => opts.UseInMemoryDatabase(db.ContextId.InstanceId.ToString()));
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(QuantDbContext))).Returns(db);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var logger = new Mock<ILogger<InstanceManager>>();
        var manager = new InstanceManager(mockScopeFactory.Object, logger.Object);
        return (manager, db);
    }

    [Fact]
    public async Task StartAsync_SetsStatusToRunning()
    {
        var (mgr, db) = CreateManager();
        var instance = new StrategyInstanceEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Symbol = "BTCUSDT",
            Status = "STOPPED"
        };
        db.StrategyInstances.Add(instance);
        await db.SaveChangesAsync();

        await mgr.StartAsync(instance.Id);

        var updated = await db.StrategyInstances.FindAsync(instance.Id);
        Assert.Equal("RUNNING", updated!.Status);
    }

    [Fact]
    public async Task StartAsync_AddsToRunningInstances()
    {
        var (mgr, db) = CreateManager();
        var id = Guid.NewGuid();
        db.StrategyInstances.Add(new StrategyInstanceEntity
        {
            Id = id, UserId = Guid.NewGuid(), Symbol = "X", Status = "STOPPED"
        });
        await db.SaveChangesAsync();

        await mgr.StartAsync(id);

        Assert.Contains(id, mgr.GetRunningInstanceIds());
    }

    [Fact]
    public async Task StartAsync_NonExistentInstance_Throws()
    {
        var (mgr, _) = CreateManager();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mgr.StartAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task StopAsync_SetsStatusToStopped()
    {
        var (mgr, db) = CreateManager();
        var id = Guid.NewGuid();
        db.StrategyInstances.Add(new StrategyInstanceEntity
        {
            Id = id, UserId = Guid.NewGuid(), Symbol = "Y", Status = "RUNNING"
        });
        await db.SaveChangesAsync();

        // First start to populate dictionary
        await mgr.StartAsync(id);
        // Re-set status (StartAsync set it to RUNNING already)
        var inst = await db.StrategyInstances.FindAsync(id);
        inst!.Status = "RUNNING";
        await db.SaveChangesAsync();

        await mgr.StopAsync(id);

        var updated = await db.StrategyInstances.FindAsync(id);
        Assert.Equal("STOPPED", updated!.Status);
    }

    [Fact]
    public async Task StopAsync_RemovesFromRunningInstances()
    {
        var (mgr, db) = CreateManager();
        var id = Guid.NewGuid();
        db.StrategyInstances.Add(new StrategyInstanceEntity
        {
            Id = id, UserId = Guid.NewGuid(), Symbol = "Z", Status = "STOPPED"
        });
        await db.SaveChangesAsync();

        await mgr.StartAsync(id);
        Assert.Contains(id, mgr.GetRunningInstanceIds());

        // Reset to STOPPED in DB for StopAsync
        var inst = await db.StrategyInstances.FindAsync(id);
        inst!.Status = "RUNNING";
        await db.SaveChangesAsync();

        await mgr.StopAsync(id);
        Assert.DoesNotContain(id, mgr.GetRunningInstanceIds());
    }

    [Fact]
    public async Task StopAsync_NonExistentInstance_DoesNotThrow()
    {
        var (mgr, _) = CreateManager();
        // Should not throw for unknown instances
        await mgr.StopAsync(Guid.NewGuid());
    }

    [Fact]
    public void GetRunningInstanceIds_InitiallyEmpty()
    {
        var (mgr, _) = CreateManager();
        Assert.Empty(mgr.GetRunningInstanceIds());
    }

    [Fact]
    public async Task RestoreRunningInstancesAsync_LoadsRunningInstances()
    {
        var (mgr, db) = CreateManager();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        db.StrategyInstances.AddRange(
            new StrategyInstanceEntity { Id = id1, UserId = Guid.NewGuid(), Symbol = "A", Status = "RUNNING" },
            new StrategyInstanceEntity { Id = id2, UserId = Guid.NewGuid(), Symbol = "B", Status = "STOPPED" }
        );
        await db.SaveChangesAsync();

        await mgr.RestoreRunningInstancesAsync();

        Assert.Contains(id1, mgr.GetRunningInstanceIds());
        Assert.DoesNotContain(id2, mgr.GetRunningInstanceIds());
    }
}
