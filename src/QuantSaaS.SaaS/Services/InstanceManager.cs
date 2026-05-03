using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// In-memory registry of running strategy instances.
/// Restored from DB on startup; state machine: STOPPED → RUNNING → STOPPED|ERROR.
/// </summary>
public class InstanceManager
{
    private readonly ConcurrentDictionary<Guid, string> _runningInstances = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstanceManager> _logger;

    public InstanceManager(IServiceScopeFactory scopeFactory, ILogger<InstanceManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RestoreRunningInstancesAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
        var running = await db.StrategyInstances
            .Where(i => i.Status == "RUNNING")
            .Select(i => i.Id)
            .ToListAsync(ct);

        foreach (var id in running)
            _runningInstances.TryAdd(id, "RUNNING");

        _logger.LogInformation("Restored {Count} running instances from database", running.Count);
    }

    public IEnumerable<Guid> GetRunningInstanceIds() => _runningInstances.Keys;

    public async Task StartAsync(Guid instanceId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
        var instance = await db.StrategyInstances.FindAsync([instanceId], ct);
        if (instance == null) throw new InvalidOperationException($"Instance {instanceId} not found");

        instance.Status = "RUNNING";
        instance.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        _runningInstances.TryAdd(instanceId, "RUNNING");
        _logger.LogInformation("Instance {Id} started", instanceId);
    }

    public async Task StopAsync(Guid instanceId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
        var instance = await db.StrategyInstances.FindAsync([instanceId], ct);
        if (instance == null) return;

        instance.Status = "STOPPED";
        await db.SaveChangesAsync(ct);
        _runningInstances.TryRemove(instanceId, out _);
        _logger.LogInformation("Instance {Id} stopped", instanceId);
    }
}
