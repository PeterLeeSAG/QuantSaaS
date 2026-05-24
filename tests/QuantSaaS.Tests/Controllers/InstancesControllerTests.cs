using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Controllers;
using QuantSaaS.SaaS.Models;
using QuantSaaS.SaaS.Services;
using System.Security.Claims;

namespace QuantSaaS.Tests.Controllers;

public class InstancesControllerTests
{
    private static QuantDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<QuantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuantDbContext(opts);
    }

    private static InstancesController CreateController(QuantDbContext db, Guid userId)
    {
        var mockMgr = new Mock<InstanceManager>(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<InstanceManager>>());
        mockMgr.Setup(m => m.StartAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        mockMgr.Setup(m => m.StopAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var ctrl = new InstancesController(db, mockMgr.Object);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "user@test.com"),
            new Claim(ClaimTypes.Role, "user"),
        }, "TestAuth");
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return ctrl;
    }

    // ── ListInstances ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListInstances_ReturnsOnlyCurrentUserInstances()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.StrategyInstances.AddRange(
            new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT", Status = "STOPPED" },
            new StrategyInstanceEntity { UserId = userId, Symbol = "ETHUSDT", Status = "RUNNING" },
            new StrategyInstanceEntity { UserId = otherUserId, Symbol = "XRPUSDT", Status = "STOPPED" }
        );
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.ListInstances();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<InstanceSummary>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task ListInstances_EmptyList_ReturnsOk()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);
        var result = await ctrl.ListInstances();
        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<InstanceSummary>>(ok.Value);
        Assert.Empty(list);
    }

    // ── CreateInstance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInstance_CreatesInstanceAndPortfolio()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);

        var req = new CreateInstanceRequest("BTCUSDT", "1D", 10000m);
        var result = await ctrl.CreateInstance(req);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await db.StrategyInstances.CountAsync());
        Assert.Equal(1, await db.PortfolioStates.CountAsync());
    }

    [Fact]
    public async Task CreateInstance_ReturnsInstanceId()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);

        var result = await ctrl.CreateInstance(new CreateInstanceRequest("BTCUSDT", "1D", 5000m));

        var ok = Assert.IsType<OkObjectResult>(result);
        // Value contains instanceId property
        var props = ok.Value!.GetType().GetProperty("instanceId");
        Assert.NotNull(props);
        var id = (Guid)props!.GetValue(ok.Value)!;
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task CreateInstance_PortfolioHasCorrectFundQuota()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);

        await ctrl.CreateInstance(new CreateInstanceRequest("BTCUSDT", "1D", 25000m));

        var portfolio = await db.PortfolioStates.SingleAsync();
        Assert.Equal(25000m, portfolio.UsdtBalance);
    }

    [Fact]
    public async Task CreateInstance_SetsBelongingUserId()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);

        await ctrl.CreateInstance(new CreateInstanceRequest("ETHUSDT", "4H", 1000m));

        var inst = await db.StrategyInstances.SingleAsync();
        Assert.Equal(userId, inst.UserId);
    }

    // ── UpdateStatus ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_Start_UpdatesStatus()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT", Status = "STOPPED" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);

        // Simulate what InstanceManager.StartAsync does: update DB status
        inst.Status = "RUNNING";
        await db.SaveChangesAsync();

        var result = await ctrl.UpdateStatus(inst.Id, new PatchStatusRequest("start"));
        var ok = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_NotFound_Returns404()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);

        var result = await ctrl.UpdateStatus(Guid.NewGuid(), new PatchStatusRequest("start"));
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_OtherUserInstance_Returns404()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = otherUserId, Symbol = "X", Status = "STOPPED" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.UpdateStatus(inst.Id, new PatchStatusRequest("start"));
        Assert.IsType<NotFoundResult>(result);
    }

    // ── DeleteInstance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteInstance_StoppedInstance_Deletes()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT", Status = "STOPPED" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.DeleteInstance(inst.Id);

        Assert.IsType<OkResult>(result);
        Assert.Equal(0, await db.StrategyInstances.CountAsync());
    }

    [Fact]
    public async Task DeleteInstance_RunningInstance_ReturnsBadRequest()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "X", Status = "RUNNING" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.DeleteInstance(inst.Id);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, await db.StrategyInstances.CountAsync()); // not deleted
    }

    [Fact]
    public async Task DeleteInstance_NotFound_Returns404()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var ctrl = CreateController(db, userId);
        var result = await ctrl.DeleteInstance(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result);
    }
}
