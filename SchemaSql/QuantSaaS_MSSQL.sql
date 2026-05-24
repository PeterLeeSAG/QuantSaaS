-- ============================================================
--  QuantSaaS – SQL Server 2022 Express Schema
--  Target: SQL Server 16.0 (2022) Express edition
--  Run this script once against an empty QuantSaaS database.
--  All statements are idempotent (IF OBJECT_ID guards).
-- ============================================================

USE QuantSaaS;
GO

-- ── users ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.users (
        id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        email               NVARCHAR(256)    NOT NULL,
        password_hash       NVARCHAR(MAX)    NOT NULL,
        subscription_plan   NVARCHAR(50)     NOT NULL CONSTRAINT df_users_plan    DEFAULT 'free',
        created_at          DATETIME2        NOT NULL,
        CONSTRAINT uq_users_email UNIQUE (email)
    );
    PRINT 'Created table: dbo.users';
END
GO

-- ── strategy_templates ────────────────────────────────────────────────────────
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
    );
    PRINT 'Created table: dbo.strategy_templates';
END
GO

-- ── strategy_instances ────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.strategy_instances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.strategy_instances (
        id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        user_id                 UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.users(id),
        template_id             UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_templates(id),
        instrument_symbol       NVARCHAR(50)     NOT NULL,
        asset_class             INT              NOT NULL,
        exchange                NVARCHAR(50)     NOT NULL,
        quote_currency          NVARCHAR(10)     NOT NULL CONSTRAINT df_si_quote   DEFAULT 'USD',
        broker_type             NVARCHAR(50)     NOT NULL,
        status                  NVARCHAR(20)     NOT NULL CONSTRAINT df_si_status  DEFAULT 'stopped',
        created_at              DATETIME2        NOT NULL,
        last_tick_at            DATETIME2        NULL,
        active_param_pack_json  NVARCHAR(MAX)    NULL
    );
    PRINT 'Created table: dbo.strategy_instances';
END
GO

-- ── portfolio_snapshots ───────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.portfolio_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.portfolio_snapshots (
        id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        instance_id                 UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
        cash_balance                DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_cash      DEFAULT 0,
        pending_settlement_amount   DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_pending   DEFAULT 0,
        dead_stack_qty              DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_dead      DEFAULT 0,
        float_stack_qty             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_float     DEFAULT 0,
        cold_sealed_qty             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_cold      DEFAULT 0,
        total_equity                DECIMAL(18,8)    NOT NULL CONSTRAINT df_ps_equity    DEFAULT 0,
        last_processed_bar_ms       BIGINT           NOT NULL CONSTRAINT df_ps_bar       DEFAULT 0,
        runtime_state_json          NVARCHAR(MAX)    NOT NULL CONSTRAINT df_ps_state     DEFAULT '{}',
        updated_at                  DATETIME2        NOT NULL
    );
    PRINT 'Created table: dbo.portfolio_snapshots';
END
GO

-- ── spot_lots ─────────────────────────────────────────────────────────────────
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
    );
    PRINT 'Created table: dbo.spot_lots';
END
GO

-- ── trade_records ─────────────────────────────────────────────────────────────
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
        fee                 DECIMAL(18,8)    NOT NULL CONSTRAINT df_tr_fee    DEFAULT 0,
        status              NVARCHAR(20)     NOT NULL,
        filled_at           DATETIME2        NOT NULL
    );
    PRINT 'Created table: dbo.trade_records';
END
GO

-- ── pending_settlements ───────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.pending_settlements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pending_settlements (
        id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        instance_id             UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.strategy_instances(id),
        client_order_id         NVARCHAR(100)    NOT NULL,
        amount                  DECIMAL(18,8)    NOT NULL,
        settlement_date_utc     DATETIME2        NOT NULL,
        is_settled              BIT              NOT NULL CONSTRAINT df_pset_settled DEFAULT 0
    );
    PRINT 'Created table: dbo.pending_settlements';
END
GO

-- ── corporate_action_records ──────────────────────────────────────────────────
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
    );
    PRINT 'Created table: dbo.corporate_action_records';
END
GO

-- ── bar_records ───────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.bar_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.bar_records (
        id              BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
        symbol          NVARCHAR(50)  NOT NULL,
        asset_class     INT           NOT NULL,
        timeframe       NVARCHAR(10)  NOT NULL,
        open_time_ms    BIGINT        NOT NULL,
        open            DECIMAL(18,8) NOT NULL,
        high            DECIMAL(18,8) NOT NULL,
        low             DECIMAL(18,8) NOT NULL,
        close           DECIMAL(18,8) NOT NULL,
        volume          DECIMAL(18,8) NOT NULL,
        CONSTRAINT uq_bar_records UNIQUE (symbol, timeframe, open_time_ms)
    );
    PRINT 'Created table: dbo.bar_records';
END
GO

-- ── instrument_records ────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.instrument_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.instrument_records (
        id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        symbol              NVARCHAR(50)     NOT NULL,
        asset_class         INT              NOT NULL,
        exchange            NVARCHAR(50)     NOT NULL,
        quote_currency      NVARCHAR(10)     NOT NULL CONSTRAINT df_ir_quote  DEFAULT 'USD',
        lot_step            DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_step   DEFAULT 1,
        lot_min             DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_min    DEFAULT 1,
        tick_size           DECIMAL(18,8)    NOT NULL CONSTRAINT df_ir_tick   DEFAULT 0.01,
        fraction_allowed    BIT              NOT NULL CONSTRAINT df_ir_frac   DEFAULT 0,
        settlement_days     INT              NOT NULL CONSTRAINT df_ir_sdays  DEFAULT 0,
        calendar_type       NVARCHAR(20)     NOT NULL CONSTRAINT df_ir_cal    DEFAULT 'crypto',
        is_active           BIT              NOT NULL CONSTRAINT df_ir_active DEFAULT 1,
        created_at          DATETIME2        NOT NULL,
        CONSTRAINT uq_instrument_symbol UNIQUE (symbol)
    );
    PRINT 'Created table: dbo.instrument_records';
END
GO

-- ── gene_records ──────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.gene_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.gene_records (
        id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        template_id     UNIQUEIDENTIFIER NOT NULL,
        asset_class     INT              NOT NULL,
        symbol          NVARCHAR(50)     NOT NULL,
        role            NVARCHAR(20)     NOT NULL CONSTRAINT df_gr_role   DEFAULT 'challenger',
        param_pack_json NVARCHAR(MAX)    NOT NULL CONSTRAINT df_gr_params DEFAULT '{}',
        score_total     DECIMAL(18,8)    NOT NULL CONSTRAINT df_gr_score  DEFAULT 0,
        max_drawdown    DECIMAL(18,8)    NOT NULL CONSTRAINT df_gr_dd     DEFAULT 0,
        evolved_at      DATETIME2        NOT NULL,
        promoted_at     DATETIME2        NULL,
        retired_at      DATETIME2        NULL
    );
    PRINT 'Created table: dbo.gene_records';
END
GO

-- ── evolution_tasks ───────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.evolution_tasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.evolution_tasks (
        id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        template_id             UNIQUEIDENTIFIER NOT NULL,
        symbol                  NVARCHAR(50)     NOT NULL,
        asset_class             INT              NOT NULL,
        status                  NVARCHAR(20)     NOT NULL CONSTRAINT df_et_status   DEFAULT 'pending',
        progress                INT              NOT NULL CONSTRAINT df_et_progress DEFAULT 0,
        config_json             NVARCHAR(MAX)    NOT NULL CONSTRAINT df_et_cfg      DEFAULT '{}',
        created_at              DATETIME2        NOT NULL,
        completed_at            DATETIME2        NULL,
        result_gene_record_id   UNIQUEIDENTIFIER NULL
    );
    PRINT 'Created table: dbo.evolution_tasks';
END
GO

-- ── audit_logs ────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_logs (
        id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        instance_id     UNIQUEIDENTIFIER NULL,
        event_type      NVARCHAR(50)     NOT NULL,
        payload_json    NVARCHAR(MAX)    NOT NULL CONSTRAINT df_al_payload DEFAULT '{}',
        created_at      DATETIME2        NOT NULL
    );
    PRINT 'Created table: dbo.audit_logs';
END
GO

-- ── equity_snapshots ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.equity_snapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.equity_snapshots (
        id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        user_id         UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.users(id),
        instance_id     UNIQUEIDENTIFIER NULL     REFERENCES dbo.strategy_instances(id),
        date_utc        DATE             NOT NULL,
        equity          DECIMAL(18,8)    NOT NULL,
        CONSTRAINT uq_equity_snapshot UNIQUE (user_id, instance_id, date_utc)
    );
    PRINT 'Created table: dbo.equity_snapshots';
END
GO

-- ============================================================
--  Seed data  (demo user + strategy templates)
--  Passwords are BCrypt hashes; login via /api/v1/auth/login.
--  This seed block is also idempotent.
-- ============================================================

-- demo user  (email: demo@quantsaas.local / password: demo1234)
IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE id = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO dbo.users (id, email, password_hash, subscription_plan, created_at)
    VALUES (
        '00000000-0000-0000-0000-000000000001',
        'demo@quantsaas.local',
        '$2a$11$PLACEHOLDER_REPLACE_WITH_BCRYPT_HASH',  -- replace with BCrypt.HashPassword("demo1234")
        'pro',
        GETUTCDATE()
    );
    PRINT 'Seeded demo user';
END
GO

-- admin user  (email: admin@quantsaas.local / password: admin1234)
-- Note: admin user is managed by EF Core (QuantDbContext). Add manually if needed:
-- INSERT INTO dbo.users (...) VALUES ('00000000-0000-0000-0000-000000000002', 'admin@quantsaas.local', ...)
GO

-- strategy templates
IF NOT EXISTS (SELECT 1 FROM dbo.strategy_templates WHERE id = '10000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO dbo.strategy_templates (id, strategy_id, name, version, asset_class, is_spot_only, manifest_json, created_at)
    VALUES
        ('10000000-0000-0000-0000-000000000001', 'btc-spot-v1', 'BTC Spot DCA',     '1.0', 0, 1, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000002', 'btc-spot-v1', 'ETH Spot DCA',     '1.0', 0, 1, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000003', 'btc-spot-v1', 'SOL Spot DCA',     '1.0', 0, 1, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000004', 'stock-v1',    'AAPL Stock Ghost', '1.0', 1, 0, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000005', 'stock-v1',    'MSFT Stock Ghost', '1.0', 1, 0, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000006', 'stock-v1',    'SPY ETF Ghost',    '1.0', 2, 0, '{}', GETUTCDATE()),
        ('10000000-0000-0000-0000-000000000007', 'stock-v1',    'QQQ ETF Ghost',    '1.0', 2, 0, '{}', GETUTCDATE());
    PRINT 'Seeded strategy templates';
END
GO

PRINT 'QuantSaaS schema initialisation complete.';
GO
