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

public class DashboardControllerTests
{
    private static QuantDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<QuantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuantDbContext(opts);
    }

    private static DashboardController CreateController(QuantDbContext db, Guid userId)
    {
        var ctrl = new DashboardController(db);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        }, "Test");
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return ctrl;
    }

    // ── GetDashboardData ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardData_InstanceNotFound_Returns404()
    {
        var db = CreateDb();
        var ctrl = CreateController(db, Guid.NewGuid());
        var result = await ctrl.GetDashboardData(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetDashboardData_NoPortfolio_ReturnsZeroEquity()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT", Status = "RUNNING" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.GetDashboardData(inst.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<DashboardData>(ok.Value);
        Assert.Equal(0m, data.TotalEquity);
    }

    [Fact]
    public async Task GetDashboardData_WithPortfolio_ReturnsCorrectEquity()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT", Status = "RUNNING" };
        db.StrategyInstances.Add(inst);
        var portfolio = new PortfolioStateEntity
        {
            InstanceId = inst.Id,
            UsdtBalance = 5000m,
            DeadBtc = 0.1m,
            FloatBtc = 0.05m
        };
        db.PortfolioStates.Add(portfolio);
        db.KLines.Add(new KLineEntity
        {
            Symbol = "BTCUSDT", Interval = "1d",
            OpenTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Close = 40000m
        });
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.GetDashboardData(inst.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<DashboardData>(ok.Value);
        // equity = 5000 + (0.1 + 0.05) * 40000 = 5000 + 6000 = 11000
        Assert.Equal(11000m, data.TotalEquity);
        Assert.Equal(5000m, data.UsdtBalance);
    }

    [Fact]
    public async Task GetDashboardData_OtherUserInstance_Returns404()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = otherUserId, Symbol = "X", Status = "STOPPED" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.GetDashboardData(inst.Id);
        Assert.IsType<NotFoundResult>(result);
    }

    // ── GetEquitySnapshots ────────────────────────────────────────────────────

    [Fact]
    public async Task GetEquitySnapshots_NoBarsOrPortfolio_ReturnsEmptyArray()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT" };
        db.StrategyInstances.Add(inst);
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.GetEquitySnapshots(inst.Id, 30);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<EquityPoint[]>(ok.Value);
        Assert.Empty((EquityPoint[])ok.Value!);
    }

    [Fact]
    public async Task GetEquitySnapshots_WithBarsAndPortfolio_ReturnsPoints()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var inst = new StrategyInstanceEntity { UserId = userId, Symbol = "BTCUSDT" };
        db.StrategyInstances.Add(inst);
        db.PortfolioStates.Add(new PortfolioStateEntity { InstanceId = inst.Id, UsdtBalance = 1000m, DeadBtc = 0.1m });
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        db.KLines.AddRange(
            new KLineEntity { Symbol = "BTCUSDT", Interval = "1d", OpenTime = nowMs - 86_400_000, Close = 50000m },
            new KLineEntity { Symbol = "BTCUSDT", Interval = "1d", OpenTime = nowMs, Close = 51000m }
        );
        await db.SaveChangesAsync();

        var ctrl = CreateController(db, userId);
        var result = await ctrl.GetEquitySnapshots(inst.Id, 30);

        var ok = Assert.IsType<OkObjectResult>(result);
        var points = Assert.IsAssignableFrom<IEnumerable<EquityPoint>>(ok.Value);
        Assert.Equal(2, points.Count());
    }

    [Fact]
    public async Task GetEquitySnapshots_InstanceNotFound_Returns404()
    {
        var db = CreateDb();
        var ctrl = CreateController(db, Guid.NewGuid());
        var result = await ctrl.GetEquitySnapshots(Guid.NewGuid(), 30);
        Assert.IsType<NotFoundResult>(result);
    }
}

public class SystemControllerTests
{
    private static SystemController CreateController(InstanceManager mgr, WsHub wsHub, Guid userId, string email = "u@t.com")
    {
        var ctrl = new SystemController(mgr, wsHub);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
        }, "Test");
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return ctrl;
    }

    private static InstanceManager CreateManager()
    {
        var logger = new Mock<ILogger<InstanceManager>>();
        return new InstanceManager(Mock.Of<IServiceScopeFactory>(), logger.Object);
    }

    private static WsHub CreateHub()
    {
        var logger = new Mock<ILogger<WsHub>>();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var jwtCfg = new QuantSaaS.Core.Config.JwtConfig { Secret = "test-secret-key-12345678901234567890!", ExpiryHours = 1 };
        var jwtSvc = new JwtService(jwtCfg);
        return new WsHub(scopeFactory.Object, jwtSvc, logger.Object);
    }

    [Fact]
    public void GetStatus_NoRunningInstances_ReturnsEngineStatusPaused()
    {
        var mgr = CreateManager();
        var hub = CreateHub();
        var ctrl = CreateController(mgr, hub, Guid.NewGuid());

        var result = ctrl.GetStatus();
        var ok = Assert.IsType<OkObjectResult>(result);
        var status = Assert.IsType<SystemStatusResponse>(ok.Value);
        Assert.Equal("paused", status.EngineStatus);
    }

    [Fact]
    public void GetStatus_AgentNotConnected_ReturnsAgentConnectedFalse()
    {
        var mgr = CreateManager();
        var hub = CreateHub();
        var ctrl = CreateController(mgr, hub, Guid.NewGuid());

        var result = ctrl.GetStatus();
        var ok = Assert.IsType<OkObjectResult>(result);
        var status = Assert.IsType<SystemStatusResponse>(ok.Value);
        Assert.False(status.AgentConnected);
    }

    [Fact]
    public void GetStatus_ReturnsEmail()
    {
        var mgr = CreateManager();
        var hub = CreateHub();
        var ctrl = CreateController(mgr, hub, Guid.NewGuid(), "test@test.com");

        var result = ctrl.GetStatus();
        var ok = Assert.IsType<OkObjectResult>(result);
        var status = Assert.IsType<SystemStatusResponse>(ok.Value);
        Assert.Equal("test@test.com", status.UserEmail);
    }

    [Fact]
    public void GetStatus_LastCheckedMs_IsApproximatelyNow()
    {
        var mgr = CreateManager();
        var hub = CreateHub();
        var ctrl = CreateController(mgr, hub, Guid.NewGuid());

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = ctrl.GetStatus();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var ok = Assert.IsType<OkObjectResult>(result);
        var status = Assert.IsType<SystemStatusResponse>(ok.Value);
        Assert.InRange(status.LastCheckedMs, before, after + 1000);
    }
}
