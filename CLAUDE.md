# QuantSaaS – AI Collaboration Constitution

## Unique Source of Truth
All functional requirements derive exclusively from `readme/` documents and the code in `src/`.
Features not defined there must not be implemented.

## Working Order
1. Strategy/backtest changes: always read relevant interfaces in `Core/Interfaces/` first.
2. DB schema changes: modify POCO classes in `Infrastructure/Models/DbModels.cs` only (Code-First / AutoMigrate, never write SQL files).
3. Price calculations: use dimensionless expressions (log-returns, ratios). Never compare absolute prices across assets.
4. Architecture boundaries: SaaS orchestrates, Strategy computes, Agent executes. Do not blur these roles.

## Iron Rules (Non-Negotiable)
1. **Strategy homomorphism**: backtest and live trading call the identical `Step()` implementation.
2. **Strategy purity**: `Step()` must never contain `DateTime.Now`, `HttpClient`, `File.Open`, `SqlConnection`, `Random`, or any I/O.
3. **API Key isolation**: broker credentials live only in `config.agent.yaml` on LocalAgent. Never in SaaS DB, environment variables on the SaaS host, or any code file.
4. **No isBacktest branches**: `Step()` must not contain `if (isBacktest)` or equivalent guards.
5. **Dimensionless features**: all signals passed to `Step()` must be ratios, log-returns, or z-scores. No absolute prices.
6. **GORM Code-First**: schema source of truth is C# POCO classes. Never write SQL migration scripts.
7. **Single Postgres + Redis**: no additional databases. Redis is cache-only, not a signal bus.
8. **Calendar in ACL only**: `IMarketCalendar` is called in the ACL/cron layer before `Step()`. Never inject it into `Step()`.

## Directory Map
| Path | Responsibility |
|---|---|
| `src/QuantSaaS.Core/` | Shared interfaces, models, calendar implementations |
| `src/QuantSaaS.Quant/` | Pure math: indicators, backtest engine, cost models, Ghost DCA |
| `src/QuantSaaS.Strategy/` | Strategy implementations (IPureStrategy); pure functions only |
| `src/QuantSaaS.Evolution/` | GA engine + evolvable adapters |
| `src/QuantSaaS.Infrastructure/` | DB models, broker connectors, data providers, WebSocket protocol |
| `src/QuantSaaS.SaaS/` | Cron tick driver, template registry, HTTP controllers |
| `src/QuantSaaS.Agent/` | LocalAgent entry point; holds API credentials, never in SaaS |

## Validation Commands
```bash
dotnet build      # must produce 0 errors, 0 warnings
dotnet test       # run all unit tests
```
