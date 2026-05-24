using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using QuantSaaS.Core.Config;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Config ────────────────────────────────────────────────────────────────────
var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig
{
    Secret = builder.Configuration["Jwt:Secret"] ?? "dev-secret-minimum-32-characters-long!",
    ExpiryHours = 24
};

// ── Database ──────────────────────────────────────────────────────────────────
// Dapper/Postgres services (primary DB layer — see Infrastructure/Services/)
var pgConnStr = builder.Configuration.GetConnectionString("MsSql");
if (!string.IsNullOrEmpty(pgConnStr))
{
    var dbFactory = new DbConnectionFactory(pgConnStr);
    builder.Services.AddSingleton(dbFactory);
    builder.Services.AddSingleton<DbInitializer>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    builder.Services.AddScoped<IInstanceService, InstanceService>();
    builder.Services.AddScoped<ITradeService, TradeService>();
    builder.Services.AddScoped<IEvolutionService, EvolutionService>();
    builder.Services.AddScoped<IUserService, UserService>();
}

// Dapper/MSSQL services — activated when a "MsSql" connection string is present
// and Postgres is not configured (SQL Server Express 2022+).
var msSqlConnStr = builder.Configuration.GetConnectionString("MsSql");
if (!string.IsNullOrEmpty(msSqlConnStr) && string.IsNullOrEmpty(pgConnStr))
{
    var msFactory = new MsSqlConnectionFactory(msSqlConnStr);
    builder.Services.AddSingleton(msFactory);
    builder.Services.AddSingleton<MsSqlDbInitializer>();
    builder.Services.AddScoped<IDashboardService, MsSqlDashboardService>();
    builder.Services.AddScoped<IInstanceService, MsSqlInstanceService>();
    builder.Services.AddScoped<ITradeService, MsSqlTradeService>();
    builder.Services.AddScoped<IEvolutionService, MsSqlEvolutionService>();
    builder.Services.AddScoped<IUserService, MsSqlUserService>();
}

// EF Core (for auth entities + WebSocket state — uses SQLite in dev, Postgres or SQL Server in prod)
var efConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=quantsaas_dev.db";

if (efConnStr.StartsWith("Data Source"))
{
    builder.Services.AddDbContext<QuantDbContext>(opts =>
        opts.UseSqlite(efConnStr));
}
else if (efConnStr.Contains("Server=") || efConnStr.Contains("server=") || efConnStr.Contains("Data Source=") && efConnStr.Contains("Initial Catalog="))
{
    builder.Services.AddDbContext<QuantDbContext>(opts =>
        opts.UseSqlServer(efConnStr));
}
else
{
    builder.Services.AddDbContext<QuantDbContext>(opts =>
        opts.UseNpgsql(efConnStr));
}

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton(jwtConfig);
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<WsHub>();
builder.Services.AddSingleton<InstanceManager>();
builder.Services.AddHostedService<CronTickService>();

// ── Blazor state (scoped per circuit) ────────────────────────────────────────
builder.Services.AddScoped<AppState>();

// ── API client for Blazor → REST calls (in-process loopback) ─────────────────
builder.Services.AddHttpClient("self", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var urls = cfg["ASPNETCORE_URLS"] ?? cfg["urls"] ?? "http://localhost:5292";
    var httpUrl = urls.Split(';').FirstOrDefault(u => u.StartsWith("http://")) ?? urls.Split(';').First();
    client.BaseAddress = new Uri(httpUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<ApiClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var appState = sp.GetRequiredService<AppState>();
    return new ApiClient(factory.CreateClient("self"), appState);
});

// ── JWT Auth ──────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtConfig.Secret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization();

// ── Blazor + API ──────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── DB initialisation (schema + seed) ────────────────────────────────────────
if (!string.IsNullOrEmpty(pgConnStr))
{
    using var scope = app.Services.CreateScope();
    var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    try
    {
        await init.InitialiseAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Postgres DB initialisation skipped: {Message}", ex.Message);
    }
}

if (!string.IsNullOrEmpty(msSqlConnStr) && string.IsNullOrEmpty(pgConnStr))
{
    using var scope = app.Services.CreateScope();
    var init = scope.ServiceProvider.GetRequiredService<MsSqlDbInitializer>();
    try
    {
        await init.InitialiseAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "MSSQL DB initialisation skipped: {Message}", ex.Message);
    }
}

// ── EF Core schema (EnsureCreated = code-first AutoMigrate; no migration files needed) ───
using (var scope = app.Services.CreateScope())
{
    try
    {
        var efDb = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
        await efDb.Database.EnsureCreatedAsync();

        // Seed admin user
        if (!efDb.Users.Any(u => u.Email == "admin@quantsaas.local"))
        {
            efDb.Users.Add(new UserEntity
            {
                Id        = new Guid("00000000-0000-0000-0000-000000000002"),
                Email     = "admin@quantsaas.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin1234"),
                Role      = "admin",
                Plan      = "pro",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Seed demo user
        if (!efDb.Users.Any(u => u.Email == "demo@quantsaas.local"))
        {
            efDb.Users.Add(new UserEntity
            {
                Id        = new Guid("00000000-0000-0000-0000-000000000001"),
                Email     = "demo@quantsaas.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
                Role      = "user",
                Plan      = "pro",
                CreatedAt = DateTime.UtcNow
            });
        }

        await efDb.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "EF Core DB init skipped: {Message}", ex.Message);
    }
}

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorComponents<QuantSaaS.SaaS.Components.App>()
   .AddInteractiveServerRenderMode();

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", role = "saas" }))
   .WithName("HealthCheck");

app.Run();
