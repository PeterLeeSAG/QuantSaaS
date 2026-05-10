using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure: DB + Services ────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

var dbFactory = new DbConnectionFactory(connStr);
builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<DbInitializer>();

builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IInstanceService, InstanceService>();
builder.Services.AddScoped<ITradeService, TradeService>();
builder.Services.AddScoped<IEvolutionService, EvolutionService>();
builder.Services.AddScoped<IUserService, UserService>();

// ── ASP.NET ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

// ── DB initialisation (schema + seed) ────────────────────────────────────────
// Runs synchronously at startup; safe because it uses CREATE TABLE IF NOT EXISTS.
using (var scope = app.Services.CreateScope())
{
    var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    try
    {
        await init.InitialiseAsync();
    }
    catch (Exception ex)
    {
        // Log and continue — the app should start even if Postgres is unavailable
        // (e.g. during CI builds without a live DB).
        app.Logger.LogWarning(ex, "DB initialisation skipped: {Message}", ex.Message);
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

app.MapControllers();
app.MapRazorPages();

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", role = "saas" }))
   .WithName("HealthCheck");

app.Run();
