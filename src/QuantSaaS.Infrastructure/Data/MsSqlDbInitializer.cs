using Dapper;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Data;

/// <summary>
/// Idempotent SQL Server schema initializer and demo seed.
/// Uses T-SQL IF OBJECT_ID guards so it is safe to run on every startup.
/// Called once at application startup when an "MsSql" connection string is present.
/// </summary>
public sealed class MsSqlDbInitializer
{
    /// <summary>Well-known seed user ID for demo/dev environments.</summary>
    public static readonly Guid SeedUserId = new("00000000-0000-0000-0000-000000000001");

    private readonly MsSqlConnectionFactory _db;

    public MsSqlDbInitializer(MsSqlConnectionFactory db)
    {
        _db = db;
    }

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        foreach (var ddl in DdlStatements)
            await conn.ExecuteAsync(ddl);

        await SeedAsync(conn);
    }

    // ── DDL ────────────────────────────────────────────────────────────────────
    // Each entry is one IF OBJECT_ID...BEGIN CREATE TABLE...END block so that
    // each can be executed as a standalone batch.

    private static readonly string[] DdlStatements =
    [
        """
        IF OBJECT_ID(N'dbo.users', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.users (
                id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                email               NVARCHAR(256)    NOT NULL,
                password_hash       NVARCHAR(MAX)    NOT NULL,
                subscription_plan   NVARCHAR(50)     NOT NULL CONSTRAINT df_users_plan DEFAULT 'free',
                created_at          DATETIME2        NOT NULL,
                CONSTRAINT uq_users_email UNIQUE (email)
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.strategy_templates', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.strategy_templates (
                id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                strategy_id     NVARCHAR(100)    NOT NULL,
                name            NVARCHAR(200)    NOT NULL,
                version         NVARCHAR(20)     NOT NULL,
                asset_class     INT              NOT NULL,
                is_spot_only    BIT              NOT NULL,
                manifest_json   NVARCHAR(MAX)    NOT NULL CONSTRAINT df_st_manifest DEFAULT '{}',
                created_at      DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.strategy_instances', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.strategy_instances (
                id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                user_id                 UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.users(id),
                template_id             UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_templates(id),
                instrument_symbol       NVARCHAR(50)     NOT NULL,
                asset_class             INT              NOT NULL,
                exchange                NVARCHAR(50)     NOT NULL,
                quote_currency          NVARCHAR(10)     NOT NULL CONSTRAINT df_si_quote DEFAULT 'USD',
                broker_type             NVARCHAR(50)     NOT NULL,
                status                  NVARCHAR(20)     NOT NULL CONSTRAINT df_si_status DEFAULT 'stopped',
                created_at              DATETIME2        NOT NULL,
                last_tick_at            DATETIME2        NULL,
                active_param_pack_json  NVARCHAR(MAX)    NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.portfolio_snapshots', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.portfolio_snapshots (
                id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                instance_id                 UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
                cash_balance                DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_cash DEFAULT 0,
                pending_settlement_amount   DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_pending DEFAULT 0,
                dead_stack_qty              DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_dead DEFAULT 0,
                float_stack_qty             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_float DEFAULT 0,
                cold_sealed_qty             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_cold DEFAULT 0,
                total_equity                DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_equity DEFAULT 0,
                last_processed_bar_ms       BIGINT           NOT NULL CONSTRAINT df_ps_bar DEFAULT 0,
                runtime_state_json          NVARCHAR(MAX)    NOT NULL CONSTRAINT df_ps_state DEFAULT '{}',
                updated_at                  DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.spot_lots', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.spot_lots (
                id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                instance_id     UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
                lot_type        NVARCHAR(50)     NOT NULL,
                quantity        DECIMAL(18,8)    NOT NULL,
                cost_price      DECIMAL(18,8)    NOT NULL,
                is_cold_sealed  BIT              NOT NULL CONSTRAINT df_sl_cold DEFAULT 0,
                acquired_at     DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.trade_records', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.trade_records (
                id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                instance_id         UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
                client_order_id     NVARCHAR(100)    NOT NULL,
                action              NVARCHAR(10)     NOT NULL,
                engine              NVARCHAR(20)     NOT NULL CONSTRAINT df_tr_engine DEFAULT '',
                symbol              NVARCHAR(50)     NOT NULL,
                asset_class         INT              NOT NULL,
                filled_qty          DECIMAL(18,8)    NOT NULL,
                filled_price        DECIMAL(18,8)    NOT NULL,
                fee                 DECIMAL(18,8)    NOT NULL CONSTRAINT df_tr_fee DEFAULT 0,
                status              NVARCHAR(20)     NOT NULL,
                filled_at           DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.pending_settlements', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.pending_settlements (
                id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                instance_id             UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
                client_order_id         NVARCHAR(100)    NOT NULL,
                amount                  DECIMAL(18,8)    NOT NULL,
                settlement_date_utc     DATETIME2        NOT NULL,
                is_settled              BIT              NOT NULL CONSTRAINT df_pset_settled DEFAULT 0
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.corporate_action_records', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.corporate_action_records (
                id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                symbol      NVARCHAR(50)     NOT NULL,
                type        NVARCHAR(50)     NOT NULL,
                ex_date_utc DATETIME2        NOT NULL,
                amount      DECIMAL(18,8)    NOT NULL,
                applied     BIT              NOT NULL CONSTRAINT df_car_applied DEFAULT 0,
                created_at  DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.bar_records', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.bar_records (
                id              BIGINT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
                symbol          NVARCHAR(50)     NOT NULL,
                asset_class     INT              NOT NULL,
                timeframe       NVARCHAR(10)     NOT NULL,
                open_time_ms    BIGINT           NOT NULL,
                open            DECIMAL(18,8)    NOT NULL,
                high            DECIMAL(18,8)    NOT NULL,
                low             DECIMAL(18,8)    NOT NULL,
                close           DECIMAL(18,8)    NOT NULL,
                volume          DECIMAL(18,8)    NOT NULL,
                CONSTRAINT uq_bar_records UNIQUE (symbol, timeframe, open_time_ms)
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.instrument_records', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.instrument_records (
                id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                symbol              NVARCHAR(50)     NOT NULL,
                asset_class         INT              NOT NULL,
                exchange            NVARCHAR(50)     NOT NULL,
                quote_currency      NVARCHAR(10)     NOT NULL CONSTRAINT df_ir_quote DEFAULT 'USD',
                lot_step            DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_step DEFAULT 1,
                lot_min             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_min DEFAULT 1,
                tick_size           DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_tick DEFAULT 0.01,
                fraction_allowed    BIT              NOT NULL CONSTRAINT df_ir_frac DEFAULT 0,
                settlement_days     INT              NOT NULL CONSTRAINT df_ir_sdays DEFAULT 0,
                calendar_type       NVARCHAR(20)     NOT NULL CONSTRAINT df_ir_cal DEFAULT 'crypto',
                is_active           BIT              NOT NULL CONSTRAINT df_ir_active DEFAULT 1,
                created_at          DATETIME2        NOT NULL,
                CONSTRAINT uq_instrument_symbol UNIQUE (symbol)
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.gene_records', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.gene_records (
                id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                template_id     UNIQUEIDENTIFIER NOT NULL,
                asset_class     INT              NOT NULL,
                symbol          NVARCHAR(50)     NOT NULL,
                role            NVARCHAR(20)     NOT NULL CONSTRAINT df_gr_role DEFAULT 'challenger',
                param_pack_json NVARCHAR(MAX)    NOT NULL CONSTRAINT df_gr_params DEFAULT '{}',
                score_total     DECIMAL(18,8)    NOT NULL CONSTRAINT df_gr_score DEFAULT 0,
                max_drawdown    DECIMAL(18,8)    NOT NULL CONSTRAINT df_gr_dd DEFAULT 0,
                evolved_at      DATETIME2        NOT NULL,
                promoted_at     DATETIME2        NULL,
                retired_at      DATETIME2        NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.evolution_tasks', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.evolution_tasks (
                id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                template_id             UNIQUEIDENTIFIER NOT NULL,
                symbol                  NVARCHAR(50)     NOT NULL,
                asset_class             INT              NOT NULL,
                status                  NVARCHAR(20)     NOT NULL CONSTRAINT df_et_status DEFAULT 'pending',
                progress                INT              NOT NULL CONSTRAINT df_et_progress DEFAULT 0,
                config_json             NVARCHAR(MAX)    NOT NULL CONSTRAINT df_et_cfg DEFAULT '{}',
                created_at              DATETIME2        NOT NULL,
                completed_at            DATETIME2        NULL,
                result_gene_record_id   UNIQUEIDENTIFIER NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.audit_logs (
                id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                instance_id     UNIQUEIDENTIFIER NULL,
                event_type      NVARCHAR(50)     NOT NULL,
                payload_json    NVARCHAR(MAX)    NOT NULL CONSTRAINT df_al_payload DEFAULT '{}',
                created_at      DATETIME2        NOT NULL
            )
        END
        """,

        """
        IF OBJECT_ID(N'dbo.equity_snapshots', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.equity_snapshots (
                id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                user_id         UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.users(id),
                instance_id     UNIQUEIDENTIFIER NULL     REFERENCES dbo.strategy_instances(id),
                date_utc        DATE             NOT NULL,
                equity          DECIMAL(18,8)    NOT NULL,
                CONSTRAINT uq_equity_snapshot UNIQUE (user_id, instance_id, date_utc)
            )
        END
        """,
    ];

    // ── Seed ──────────────────────────────────────────────────────────────────

    private static async Task SeedAsync(System.Data.IDbConnection conn)
    {
        // Idempotent: only seed if the seed user does not exist yet.
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.users WHERE id = @Id",
            new { Id = SeedUserId });

        if (exists > 0) return;

        // ── Seed user ─────────────────────────────────────────────────────────
        await conn.ExecuteAsync("""
            INSERT INTO dbo.users (id, email, password_hash, subscription_plan, created_at)
            VALUES (@Id, @Email, @PasswordHash, @Plan, @CreatedAt)
            """,
            new
            {
                Id = SeedUserId,
                Email = "demo@quantsaas.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
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
            new { Id = btcTemplateId,  StrategyId = "btc-spot-v1", Name = "BTC Spot DCA",     Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = ethTemplateId,  StrategyId = "btc-spot-v1", Name = "ETH Spot DCA",     Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = solTemplateId,  StrategyId = "btc-spot-v1", Name = "SOL Spot DCA",     Version = "1.0", AssetClass = (int)AssetClass.Crypto, IsSpotOnly = true,  CreatedAt = DateTime.UtcNow },
            new { Id = aaplTemplateId, StrategyId = "stock-v1",    Name = "AAPL Stock Ghost", Version = "1.0", AssetClass = (int)AssetClass.Stock,  IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = msftTemplateId, StrategyId = "stock-v1",    Name = "MSFT Stock Ghost", Version = "1.0", AssetClass = (int)AssetClass.Stock,  IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = spyTemplateId,  StrategyId = "stock-v1",    Name = "SPY ETF Ghost",    Version = "1.0", AssetClass = (int)AssetClass.ETF,    IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
            new { Id = qqqTemplateId,  StrategyId = "stock-v1",    Name = "QQQ ETF Ghost",    Version = "1.0", AssetClass = (int)AssetClass.ETF,    IsSpotOnly = false, CreatedAt = DateTime.UtcNow },
        };

        foreach (var t in templates)
        {
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.strategy_templates WHERE id = @Id)
                    INSERT INTO dbo.strategy_templates (id, strategy_id, name, version, asset_class, is_spot_only, manifest_json, created_at)
                    VALUES (@Id, @StrategyId, @Name, @Version, @AssetClass, @IsSpotOnly, '{}', @CreatedAt)
                """, t);
        }

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

        foreach (var inst in instances)
        {
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.strategy_instances WHERE id = @Id)
                    INSERT INTO dbo.strategy_instances
                        (id, user_id, template_id, instrument_symbol, asset_class, exchange, quote_currency, broker_type, status, created_at, last_tick_at)
                    VALUES
                        (@Id, @UserId, @TemplateId, @Symbol, @AssetClass, @Exchange, @QuoteCurrency, @BrokerType, @Status, @CreatedAt, @LastTickAt)
                """, inst);
        }

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

        foreach (var p in portfolios)
        {
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.portfolio_snapshots WHERE id = @Id)
                    INSERT INTO dbo.portfolio_snapshots
                        (id, instance_id, cash_balance, pending_settlement_amount, dead_stack_qty, float_stack_qty, cold_sealed_qty, total_equity, last_processed_bar_ms, runtime_state_json, updated_at)
                    VALUES
                        (@Id, @InstanceId, @Cash, @PendingSettlement, @DeadQty, @FloatQty, @ColdQty, @Equity, 0, '{}', @UpdatedAt)
                """, p);
        }

        // ── Seed 30-day equity snapshots ──────────────────────────────────────
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
        for (int d = 29; d >= 0; d--)
        {
            var date = now.AddDays(-d).Date;
            decimal userTotal = 0;
            foreach (var (instId, baseEquity) in instances)
            {
                var delta = (decimal)((d % 7) * 50 - 100);
                var instEquity = Math.Max(baseEquity + delta, 0);
                userTotal += instEquity;
                var row = new { Id = Guid.NewGuid(), UserId = SeedUserId, InstanceId = (Guid?)instId, DateUtc = date, Equity = instEquity };
                await conn.ExecuteAsync("""
                    IF NOT EXISTS (SELECT 1 FROM dbo.equity_snapshots WHERE user_id = @UserId AND instance_id = @InstanceId AND date_utc = @DateUtc)
                        INSERT INTO dbo.equity_snapshots (id, user_id, instance_id, date_utc, equity)
                        VALUES (@Id, @UserId, @InstanceId, @DateUtc, @Equity)
                    """, row);
            }
            var aggRow = new { Id = Guid.NewGuid(), UserId = SeedUserId, InstanceId = (Guid?)null, DateUtc = date, Equity = userTotal };
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.equity_snapshots WHERE user_id = @UserId AND instance_id IS NULL AND date_utc = @DateUtc)
                    INSERT INTO dbo.equity_snapshots (id, user_id, instance_id, date_utc, equity)
                    VALUES (@Id, @UserId, @InstanceId, @DateUtc, @Equity)
                """, aggRow);
        }
    }

    private static async Task SeedTradesAsync(
        System.Data.IDbConnection conn,
        dynamic[] instances,
        DateTime now)
    {
        var rng = new Random(42);
        foreach (var inst in instances)
        {
            bool isCrypto = inst.AssetClass == (int)AssetClass.Crypto;
            for (int t = 0; t < 20; t++)
            {
                var trade = new
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
                    FilledAt = now.AddHours(-(t * 4))
                };
                await conn.ExecuteAsync("""
                    IF NOT EXISTS (SELECT 1 FROM dbo.trade_records WHERE id = @Id)
                        INSERT INTO dbo.trade_records
                            (id, instance_id, client_order_id, action, engine, symbol, asset_class, filled_qty, filled_price, fee, status, filled_at)
                        VALUES
                            (@Id, @InstanceId, @ClientOrderId, @Action, @Engine, @Symbol, @AssetClass, @FilledQty, @FilledPrice, @Fee, @Status, @FilledAt)
                    """, trade);
            }
        }
    }

    private static async Task SeedGenesAsync(
        System.Data.IDbConnection conn,
        dynamic[] instances,
        DateTime now)
    {
        var rng = new Random(7);
        foreach (var inst in instances)
        {
            var gene = new
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
            };
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.gene_records WHERE id = @Id)
                    INSERT INTO dbo.gene_records
                        (id, template_id, asset_class, symbol, role, param_pack_json, score_total, max_drawdown, evolved_at, promoted_at)
                    VALUES
                        (@Id, @TemplateId, @AssetClass, @Symbol, @Role, @ParamPackJson, @ScoreTotal, @MaxDrawdown, @EvolvedAt, @PromotedAt)
                """, gene);
        }
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
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[3].TemplateId, Symbol = (string)instances[3].Symbol, AssetClass = (int)instances[3].AssetClass, Status = "pending",   Progress = 0,   ConfigJson = "{}", CreatedAt = now.AddMinutes(-30), CompletedAt = (DateTime?)null },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[1].TemplateId, Symbol = (string)instances[1].Symbol, AssetClass = (int)instances[1].AssetClass, Status = "completed", Progress = 100, ConfigJson = "{}", CreatedAt = now.AddDays(-12),   CompletedAt = (DateTime?)now.AddDays(-12).AddHours(4) },
            new { Id = Guid.NewGuid(), TemplateId = (Guid)instances[5].TemplateId, Symbol = (string)instances[5].Symbol, AssetClass = (int)instances[5].AssetClass, Status = "failed",    Progress = 22,  ConfigJson = "{}", CreatedAt = now.AddDays(-2),    CompletedAt = (DateTime?)now.AddDays(-2).AddHours(1) },
        };

        foreach (var task in tasks)
        {
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dbo.evolution_tasks WHERE id = @Id)
                    INSERT INTO dbo.evolution_tasks
                        (id, template_id, symbol, asset_class, status, progress, config_json, created_at, completed_at)
                    VALUES
                        (@Id, @TemplateId, @Symbol, @AssetClass, @Status, @Progress, @ConfigJson, @CreatedAt, @CompletedAt)
                """, task);
        }
    }
}
