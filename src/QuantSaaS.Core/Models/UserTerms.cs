namespace QuantSaaS.Core.Models;

/// <summary>
/// User-facing terminology mapping.
/// Iron Rule: No internal terms, Greek letters without explanation, or math symbols
/// may appear in any UI-facing string.
/// </summary>
public static class UserTerms
{
    // ── Portfolio ─────────────────────────────────────────────────────────────

    // Crypto
    public const string DeadBTC = "Long-term Holdings";
    public const string FloatBTC = "Active Position";
    public const string ColdSealed = "Sealed Assets";
    public const string TotalEquity = "Total Assets";
    public const string SpendableUSDT = "Available Funds";

    // Stock / ETF (same semantic roles, renamed for clarity)
    public const string DeadStackStock = "Long-term Holdings";
    public const string FloatStackStock = "Active Position";
    public const string ColdSealedStock = "Sealed Holdings";
    public const string PendingSettlement = "Funds Pending Settlement";
    public const string DividendIncome = "Dividend Received";
    public const string CorporateAction = "Account Adjustment";

    // ── Instance states ───────────────────────────────────────────────────────

    public const string Running = "Running";
    public const string Stopped = "Paused";
    public const string Error = "Error";

    // ── Trading limits ────────────────────────────────────────────────────────

    public const string PDTRule = "Day Trading Limit";
    public const string MarketClosed = "Market Currently Closed";

    // ── Evolution roles ───────────────────────────────────────────────────────

    public const string Challenger = "Candidate Parameters";
    public const string Champion = "Current Best Parameters";
    public const string Retired = "Archived";

    // ── GA / engine ───────────────────────────────────────────────────────────

    public const string EvolutionTask = "Parameter Optimisation";
    public const string StepTrigger = "Strategy Decision";
    public const string Backtest = "Historical Simulation";
    public const string GhostDCACrypto = "Passive DCA Baseline";
    public const string GhostDCAStock = "Monthly Investment Baseline";

    // ── Market ────────────────────────────────────────────────────────────────

    public const string MarketStateLabel = "Market Environment";

    // ── Infrastructure ────────────────────────────────────────────────────────

    public const string Agent = "Execution Client";
    public const string CronTick = "Scheduled Decision";
}
