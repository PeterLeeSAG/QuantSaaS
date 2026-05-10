using Dapper;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Data;

/// <summary>
/// Idempotent schema initializer (CREATE TABLE IF NOT EXISTS) and demo seed.
/// Called once at application startup.
/// </summary>
public sealed class DbInitializer
{
    /// <summary>Well-known seed user ID for demo/dev environments.</summary>
    public static readonly Guid SeedUserId = new("00000000-0000-0000-0000-000000000001");

    private readonly DbConnectionFactory _db;

    public DbInitializer(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        await conn.ExecuteAsync(Ddl);
        await SeedAsync(conn);
    }

    // ── DDL ───────────────────────────────────────────────────────────────────

    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS users (
            id                  UUID        PRIMARY KEY,
            email               TEXT        NOT NULL UNIQUE,
            password_hash       TEXT        NOT NULL,
            subscription_plan   TEXT        NOT NULL DEFAULT 'free',
            created_at          TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS strategy_templates (
            id              UUID        PRIMARY KEY,
            strategy_id     TEXT        NOT NULL,
            name            TEXT        NOT NULL,
            version         TEXT        NOT NULL,
            asset_class     INT         NOT NULL,
            is_spot_only    BOOLEAN     NOT NULL,
            manifest_json   TEXT        NOT NULL DEFAULT '{}',
            created_at      TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS strategy_instances (
            id                      UUID        PRIMARY KEY,
            user_id                 UUID        NOT NULL REFERENCES users(id),
            template_id             UUID        NOT NULL REFERENCES strategy_templates(id),
            instrument_symbol       TEXT        NOT NULL,
            asset_class             INT         NOT NULL,
            exchange                TEXT        NOT NULL,
            quote_currency          TEXT        NOT NULL DEFAULT 'USD',
            broker_type             TEXT        NOT NULL,
            status                  TEXT        NOT NULL DEFAULT 'stopped',
            created_at              TIMESTAMPTZ NOT NULL,
            last_tick_at            TIMESTAMPTZ,
            active_param_pack_json  TEXT
        );

        CREATE TABLE IF NOT EXISTS portfolio_snapshots (
            id                          UUID        PRIMARY KEY,
            instance_id                 UUID        NOT NULL REFERENCES strategy_instances(id),
            cash_balance                NUMERIC     NOT NULL DEFAULT 0,
            pending_settlement_amount   NUMERIC     NOT NULL DEFAULT 0,
            dead_stack_qty              NUMERIC     NOT NULL DEFAULT 0,
            float_stack_qty             NUMERIC     NOT NULL DEFAULT 0,
            cold_sealed_qty             NUMERIC     NOT NULL DEFAULT 0,
            total_equity                NUMERIC     NOT NULL DEFAULT 0,
            last_processed_bar_ms       BIGINT      NOT NULL DEFAULT 0,
            runtime_state_json          TEXT        NOT NULL DEFAULT '{}',
            updated_at                  TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS spot_lots (
            id              UUID        PRIMARY KEY,
            instance_id     UUID        NOT NULL REFERENCES strategy_instances(id),
            lot_type        TEXT        NOT NULL,
            quantity        NUMERIC     NOT NULL,
            cost_price      NUMERIC     NOT NULL,
            is_cold_sealed  BOOLEAN     NOT NULL DEFAULT FALSE,
            acquired_at     TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS trade_records (
            id                  UUID        PRIMARY KEY,
            instance_id         UUID        NOT NULL REFERENCES strategy_instances(id),
            client_order_id     TEXT        NOT NULL,
            action              TEXT        NOT NULL,
            engine              TEXT        NOT NULL DEFAULT '',
            symbol              TEXT        NOT NULL,
            asset_class         INT         NOT NULL,
            filled_qty          NUMERIC     NOT NULL,
            filled_price        NUMERIC     NOT NULL,
            fee                 NUMERIC     NOT NULL DEFAULT 0,
            status              TEXT        NOT NULL,
            filled_at           TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS pending_settlements (
            id                      UUID        PRIMARY KEY,
            instance_id             UUID        NOT NULL REFERENCES strategy_instances(id),
            client_order_id         TEXT        NOT NULL,
            amount                  NUMERIC     NOT NULL,
            settlement_date_utc     TIMESTAMPTZ NOT NULL,
            is_settled              BOOLEAN     NOT NULL DEFAULT FALSE
        );

        CREATE TABLE IF NOT EXISTS corporate_action_records (
            id          UUID        PRIMARY KEY,
            symbol      TEXT        NOT NULL,
            type        TEXT        NOT NULL,
            ex_date_utc TIMESTAMPTZ NOT NULL,
            amount      NUMERIC     NOT NULL,
            applied     BOOLEAN     NOT NULL DEFAULT FALSE,
            created_at  TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS bar_records (
            id          BIGSERIAL   PRIMARY KEY,
            symbol      TEXT        NOT NULL,
            asset_class INT         NOT NULL,
            timeframe   TEXT        NOT NULL,
            open_time_ms BIGINT     NOT NULL,
            open        NUMERIC     NOT NULL,
            high        NUMERIC     NOT NULL,
            low         NUMERIC     NOT NULL,
            close       NUMERIC     NOT NULL,
            volume      NUMERIC     NOT NULL,
            UNIQUE (symbol, timeframe, open_time_ms)
        );

        CREATE TABLE IF NOT EXISTS instrument_records (
            id                  UUID        PRIMARY KEY,
            symbol              TEXT        NOT NULL UNIQUE,
            asset_class         INT         NOT NULL,
            exchange            TEXT        NOT NULL,
            quote_currency      TEXT        NOT NULL DEFAULT 'USD',
            lot_step            NUMERIC     NOT NULL DEFAULT 1,
            lot_min             NUMERIC     NOT NULL DEFAULT 1,
            tick_size           NUMERIC     NOT NULL DEFAULT 0.01,
            fraction_allowed    BOOLEAN     NOT NULL DEFAULT FALSE,
            settlement_days     INT         NOT NULL DEFAULT 0,
            calendar_type       TEXT        NOT NULL DEFAULT 'crypto',
            is_active           BOOLEAN     NOT NULL DEFAULT TRUE,
            created_at          TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS gene_records (
            id              UUID        PRIMARY KEY,
            template_id     UUID        NOT NULL,
            asset_class     INT         NOT NULL,
            symbol          TEXT        NOT NULL,
            role            TEXT        NOT NULL DEFAULT 'challenger',
            param_pack_json TEXT        NOT NULL DEFAULT '{}',
            score_total     NUMERIC     NOT NULL DEFAULT 0,
            max_drawdown    NUMERIC     NOT NULL DEFAULT 0,
            evolved_at      TIMESTAMPTZ NOT NULL,
            promoted_at     TIMESTAMPTZ,
            retired_at      TIMESTAMPTZ
        );

        CREATE TABLE IF NOT EXISTS evolution_tasks (
            id                      UUID        PRIMARY KEY,
            template_id             UUID        NOT NULL,
            symbol                  TEXT        NOT NULL,
            asset_class             INT         NOT NULL,
            status                  TEXT        NOT NULL DEFAULT 'pending',
            progress                INT         NOT NULL DEFAULT 0,
            config_json             TEXT        NOT NULL DEFAULT '{}',
            created_at              TIMESTAMPTZ NOT NULL,
            completed_at            TIMESTAMPTZ,
            result_gene_record_id   UUID
        );

        CREATE TABLE IF NOT EXISTS audit_logs (
            id              UUID        PRIMARY KEY,
            instance_id     UUID,
            event_type      TEXT        NOT NULL,
            payload_json    TEXT        NOT NULL DEFAULT '{}',
            created_at      TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS equity_snapshots (
            id              UUID        PRIMARY KEY,
            user_id         UUID        NOT NULL REFERENCES users(id),
            instance_id     UUID        REFERENCES strategy_instances(id),
            date_utc        DATE        NOT NULL,
            equity          NUMERIC     NOT NULL,
            UNIQUE (user_id, instance_id, date_utc)
        );
        """;

    // ── Seed ─────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(System.Data.IDbConnection conn)
    {
        // Idempotent: only insert if the seed user does not exist yet.
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM users WHERE id = @Id",
            new { Id = SeedUserId });

        if (exists > 0) return;

        // ── Seed user ─────────────────────────────────────────────────────────
        // Password is "demo1234" – BCrypt hash stored for reference; real auth
        // should call BCrypt.Verify at login time. For now the hash is stored
        // as a plain marker so the DB schema is exercised without a BCrypt dep.
        await conn.ExecuteAsync("""
            INSERT INTO users (id, email, password_hash, subscription_plan, created_at)
            VALUES (@Id, @Email, @PasswordHash, @Plan, @CreatedAt)
            """,
            new
            {
                Id = SeedUserId,
                Email = "demo@quantsaas.local",
                PasswordHash = "$demo$demo1234",   // placeholder — replace with real BCrypt hash at login
                Plan = "pro",
                CreatedAt = DateTime.UtcNow
            });

        // ── Seed strategy templates ───────────────────────────────────────────
        var btcTemplateId  = new Guid("10000000-0000-0000-0000-000000000001");
        var ethTemplateId  = new Guid("10000000-0000-0000-0000-000000000002");
        var solTemplateId  = new Guid("10000000-0000-0000-0000-000000000003");
        var aaplTemplateId = new Guid("10000000-0000-0000-0000-000000000004");
        var msftTemplateId = new Guid("10000000-0000-0000-0000-000000000005");
        var spyTemplateId  = new Guid("10000000-0000-0000-0000-000000000006");
        var qqqTemplateId  = new Guid("10000000-0000-0000-0000-000000000007");

        var templates = new[]
        {
            new { Id = btcTemplateId,  StrategyId = "btc-spot-v1",   Name = "BTC Spot DCA",    Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = ethTemplateId,  StrategyId = "btc-spot-v1",   Name = "ETH Spot DCA",    Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = solTemplateId,  StrategyId = "btc-spot-v1",   Name = "SOL Spot DCA",    Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = aaplTemplateId, StrategyId = "stock-v1",      Name = "AAPL Stock Ghost", Version = "1.0", AssetClass = (int)AssetClass.Stock,  IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = msftTemplateId, StrategyId = "stock-v1",      Name = "MSFT Stock Ghost", Version = "1.0", AssetClass = (int)AssetClass.Stock,  IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = spyTemplateId,  StrategyId = "stock-v1",      Name = "SPY ETF Ghost",    Version = "1.0", AssetClass = (int)AssetClass.ETF,    IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = qqqTemplateId,  StrategyId = "stock-v1",      Name = "QQQ ETF Ghost",    Version = "1.0", AssetClass = (int)AssetClass.ETF,    IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
        };

        await conn.ExecuteAsync("""
            INSERT INTO strategy_templates (id, strategy_id, name, version, asset_class, is_spot_only, manifest_json, created_at)
            VALUES (@Id, @StrategyId, @Name, @Version, @AssetClass, @IsSpotOnly, '{}', @CreatedAt)
            ON CONFLICT DO NOTHING
            """, templates);

        // ── Seed instances ────────────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var instances = new[]
        {
            new { Id = new Guid("20000000-0000-0000-0000-000000000001"), UserId = SeedUserId, TemplateId = btcTemplateId,  Symbol = "BTC/USDT", AssetClass = (int)AssetClass.Crypto, Exchange = "OKX",    QuoteCurrency = "USDT", BrokerType = "okx",    Status = "running", CreatedAt = now.AddDays(-45), LastTickAt = (DateTime?)now.AddMinutes(-3) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000002"), UserId = SeedUserId, TemplateId = ethTemplateId,  Symbol = "ETH/USDT", AssetClass = (int)AssetClass.Crypto, Exchange = "OKX",    QuoteCurrency = "USDT", BrokerType = "okx",    Status = "running", CreatedAt = now.AddDays(-30), LastTickAt = (DateTime?)now.AddMinutes(-3) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000003"), UserId = SeedUserId, TemplateId = solTemplateId,  Symbol = "SOL/USDT", AssetClass = (int)AssetClass.Crypto, Exchange = "OKX",    QuoteCurrency = "USDT", BrokerType = "okx",    Status = "stopped", CreatedAt = now.AddDays(-20), LastTickAt = (DateTime?)now.AddHours(-6) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000004"), UserId = SeedUserId, TemplateId = aaplTemplateId, Symbol = "AAPL",     AssetClass = (int)AssetClass.Stock,  Exchange = "NASDAQ", QuoteCurrency = "USD",  BrokerType = "alpaca", Status = "stopped", CreatedAt = now.AddDays(-15), LastTickAt = (DateTime?)now.AddHours(-2) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000005"), UserId = SeedUserId, TemplateId = msftTemplateId, Symbol = "MSFT",     AssetClass = (int)AssetClass.Stock,  Exchange = "NASDAQ", QuoteCurrency = "USD",  BrokerType = "alpaca", Status = "error",   CreatedAt = now.AddDays(-10), LastTickAt = (DateTime?)now.AddHours(-1) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000006"), UserId = SeedUserId, TemplateId = spyTemplateId,  Symbol = "SPY",      AssetClass = (int)AssetClass.ETF,    Exchange = "NYSE",   QuoteCurrency = "USD",  BrokerType = "ibkr",   Status = "running", CreatedAt = now.AddDays(-8),  LastTickAt = (DateTime?)now.AddMinutes(-5) },
            new { Id = new Guid("20000000-0000-0000-0000-000000000007"), UserId = SeedUserId, TemplateId = qqqTemplateId,  Symbol = "QQQ",      AssetClass = (int)AssetClass.ETF,    Exchange = "NASDAQ", QuoteCurrency = "USD",  BrokerType = "ibkr",   Status = "stopped", CreatedAt = now.AddDays(-5),  LastTickAt = (DateTime?)now.AddDays(-1) },
        };

        await conn.ExecuteAsync("""
            INSERT INTO strategy_instances
                (id, user_id, template_id, instrument_symbol, asset_class, exchange, quote_currency, broker_type, status, created_at, last_tick_at)
            VALUES
                (@Id, @UserId, @TemplateId, @Symbol, @AssetClass, @Exchange, @QuoteCurrency, @BrokerType, @Status, @CreatedAt, @LastTickAt)
            ON CONFLICT DO NOTHING
            """, instances);

        // ── Seed portfolio snapshots ──────────────────────────────────────────
        var portfolios = new[]
        {
            new { Id = Guid.NewGuid(), InstanceId = instances[0].Id, Cash = 4820.30m, PendingSettlement = 0m, DeadQty = 0.15423m, FloatQty = 0.03201m, ColdQty = 0.05000m, Equity = 18420.50m, UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[1].Id, Cash = 2100.00m, PendingSettlement = 0m, DeadQty = 1.2m,     FloatQty = 0.5m,     ColdQty = 0.2m,     Equity = 9881.00m,  UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[2].Id, Cash = 1000.00m, PendingSettlement = 0m, DeadQty = 5.0m,     FloatQty = 1.5m,     ColdQty = 0m,       Equity = 3100.00m,  UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[3].Id, Cash = 5000.00m, PendingSettlement = 0m, DeadQty = 30.0m,    FloatQty = 5.0m,     ColdQty = 0m,       Equity = 12340.00m, UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[4].Id, Cash = 2000.00m, PendingSettlement = 0m, DeadQty = 18.0m,    FloatQty = 3.0m,     ColdQty = 0m,       Equity = 7200.00m,  UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[5].Id, Cash = 2500.00m, PendingSettlement = 0m, DeadQty = 15.0m,    FloatQty = 2.0m,     ColdQty = 0m,       Equity = 9960.00m,  UpdatedAt = now },
            new { Id = Guid.NewGuid(), InstanceId = instances[6].Id, Cash = 1500.00m, PendingSettlement = 0m, DeadQty = 10.0m,    FloatQty = 1.0m,     ColdQty = 0m,       Equity = 5400.00m,  UpdatedAt = now },
        };

        await conn.ExecuteAsync("""
            INSERT INTO portfolio_snapshots
                (id, instance_id, cash_balance, pending_settlement_amount, dead_stack_qty, float_stack_qty, cold_sealed_qty, total_equity, last_processed_bar_ms, runtime_state_json, updated_at)
            VALUES
                (@Id, @InstanceId, @Cash, @PendingSettlement, @DeadQty, @FloatQty, @ColdQty, @Equity, 0, '{}', @UpdatedAt)
            ON CONFLICT DO NOTHING
            """, portfolios);

        // ── Seed 30-day equity snapshots (user aggregate + per instance) ───────
        // Map instance ID → base equity from the portfolios array.
        var equityByInstance = portfolios.Select(p => (p.InstanceId, p.Equity)).ToArray();
        await SeedEquityCurveAsync(conn, equityByInstance, now);

        // ── Seed trade records ────────────────────────────────────────────────
        await SeedTradesAsync(conn, instances, now);

        // ── Seed gene records (champions) ─────────────────────────────────────
        await SeedGenesAsync(conn, instances, now);

        // ── Seed evolution tasks ──────────────────────────────────────────────
        await SeedEvolutionTasksAsync(conn, instances, now);
    }

    private static async Task SeedEquityCurveAsync(
        System.Data.IDbConnection conn,
        (Guid Id, decimal Equity)[] instances,
        DateTime now)
    {
        var rows = new List<object>();
        for (int d = 29; d >= 0; d--)
        {
            var date = now.AddDays(-d).Date;
            decimal userTotal = 0;
            foreach (var (instId, baseEquity) in instances)
            {
                var delta = (decimal)((d % 7) * 50 - 100);
                var instEquity = Math.Max(baseEquity + delta, 0);
                userTotal += instEquity;
                rows.Add(new { Id = Guid.NewGuid(), UserId = SeedUserId, InstanceId = (Guid?)instId, DateUtc = date, Equity = instEquity });
            }
            rows.Add(new { Id = Guid.NewGuid(), UserId = SeedUserId, InstanceId = (Guid?)null, DateUtc = date, Equity = userTotal });
        }

        await conn.ExecuteAsync("""
            INSERT INTO equity_snapshots (id, user_id, instance_id, date_utc, equity)
            VALUES (@Id, @UserId, @InstanceId, @DateUtc, @Equity)
            ON CONFLICT DO NOTHING
            """, rows);
    }

    private static async Task SeedTradesAsync(
        System.Data.IDbConnection conn,
        dynamic[] instances,
        DateTime now)
    {
        var trades = new List<object>();
        var rng = new Random(42);
        for (int i = 0; i < instances.Length; i++)
        {
            var inst = instances[i];
            for (int t = 0; t < 20; t++)
            {
                bool isCrypto = inst.AssetClass == (int)AssetClass.Crypto;
                trades.Add(new
                {
                    Id = Guid.NewGuid(),
                    InstanceId = inst.Id,
                    ClientOrderId = $"ord-{inst.Id}-{t}",
                    Action = t % 3 == 0 ? "SELL" : "BUY",
                    Engine = t % 2 == 0 ? "MACRO" : "MICRO",
                    Symbol = (string)inst.Symbol,
                    AssetClass = (int)inst.AssetClass,
                    FilledQty = Math.Round((decimal)(rng.NextDouble() * 0.5 + 0.001), 5),
                    FilledPrice = isCrypto
                        ? Math.Round(60000m + (decimal)(rng.NextDouble() * 3000 - 1500), 2)
                        : Math.Round(150m + (decimal)(rng.NextDouble() * 50 - 25), 2),
                    Fee = Math.Round((decimal)(rng.NextDouble() * 1.5), 4),
                    Status = "filled",
                    FilledAt = now.AddHours(-(t * 4 + i * 2))
                });
            }
        }

        await conn.ExecuteAsync("""
            INSERT INTO trade_records
                (id, instance_id, client_order_id, action, engine, symbol, asset_class, filled_qty, filled_price, fee, status, filled_at)
            VALUES
                (@Id, @InstanceId, @ClientOrderId, @Action, @Engine, @Symbol, @AssetClass, @FilledQty, @FilledPrice, @Fee, @Status, @FilledAt)
            ON CONFLICT DO NOTHING
            """, trades);
    }

    private static async Task SeedGenesAsync(
        System.Data.IDbConnection conn,
        dynamic[] instances,
        DateTime now)
    {
        var genes = new List<object>();
        var rng = new Random(7);
        foreach (var inst in instances)
        {
            genes.Add(new
            {
                Id = Guid.NewGuid(),
                TemplateId = (Guid)inst.TemplateId,
                AssetClass = (int)inst.AssetClass,
                Symbol = (string)inst.Symbol,
                Role = "champion",
                ParamPackJson = "{}",
                ScoreTotal = Math.Round((decimal)(rng.NextDouble() * 1.5 + 0.5), 3),
                MaxDrawdown = Math.Round((decimal)(rng.NextDouble() * 0.15 + 0.05), 3),
                EvolvedAt = now.AddDays(-rng.Next(1, 15)),
                PromotedAt = (DateTime?)now.AddDays(-rng.Next(1, 10))
            });
        }

        await conn.ExecuteAsync("""
            INSERT INTO gene_records
                (id, template_id, asset_class, symbol, role, param_pack_json, score_total, max_drawdown, evolved_at, promoted_at)
            VALUES
                (@Id, @TemplateId, @AssetClass, @Symbol, @Role, @ParamPackJson, @ScoreTotal, @MaxDrawdown, @EvolvedAt, @PromotedAt)
            ON CONFLICT DO NOTHING
            """, genes);
    }

    private static async Task SeedEvolutionTasksAsync(
        System.Data.IDbConnection conn,
        dynamic[] instances,
        DateTime now)
    {
        var tasks = new[]
        {
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[0].TemplateId, Symbol = (string)instances[0].Symbol, AssetClass = (int)instances[0].AssetClass, Status = "completed", Progress = 100, ConfigJson = "{}", CreatedAt = now.AddDays(-5),    CompletedAt = (DateTime?)now.AddDays(-5).AddHours(3) },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[1].TemplateId, Symbol = (string)instances[1].Symbol, AssetClass = (int)instances[1].AssetClass, Status = "running",   Progress = 63,  ConfigJson = "{}", CreatedAt = now.AddHours(-2),  CompletedAt = (DateTime?)null },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[3].TemplateId, Symbol = (string)instances[3].Symbol, AssetClass = (int)instances[3].AssetClass, Status = "pending",   Progress = 0,   ConfigJson = "{}", CreatedAt = now.AddMinutes(-30),CompletedAt = (DateTime?)null },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[1].TemplateId, Symbol = (string)instances[1].Symbol, AssetClass = (int)instances[1].AssetClass, Status = "completed", Progress = 100, ConfigJson = "{}", CreatedAt = now.AddDays(-12),   CompletedAt = (DateTime?)now.AddDays(-12).AddHours(4) },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[5].TemplateId, Symbol = (string)instances[5].Symbol, AssetClass = (int)instances[5].AssetClass, Status = "failed",    Progress = 22,  ConfigJson = "{}", CreatedAt = now.AddDays(-2),    CompletedAt = (DateTime?)now.AddDays(-2).AddHours(1) },
        };

        await conn.ExecuteAsync("""
            INSERT INTO evolution_tasks
                (id, template_id, symbol, asset_class, status, progress, config_json, created_at, completed_at)
            VALUES
                (@Id, @TemplateId, @Symbol, @AssetClass, @Status, @Progress, @ConfigJson, @CreatedAt, @CompletedAt)
            ON CONFLICT DO NOTHING
            """, tasks);
    }
}
