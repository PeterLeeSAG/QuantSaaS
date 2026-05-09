using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using QuantSaaS.Core.Config;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Services;

var builder = WebApplication.CreateBuilder(args);

// Config
var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig
{
    Secret = builder.Configuration["Jwt:Secret"] ?? "dev-secret-minimum-32-characters-long!",
    ExpiryHours = 24
};

// Database (SQLite for dev, Postgres for prod)
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=quantsaas_dev.db";

if (connStr.StartsWith("Data Source"))
{
    builder.Services.AddDbContext<QuantDbContext>(opts =>
        opts.UseSqlite(connStr));
}
else
{
    builder.Services.AddDbContext<QuantDbContext>(opts =>
        opts.UseNpgsql(connStr));
}

// Core services
builder.Services.AddSingleton(jwtConfig);
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<WsHub>();
builder.Services.AddSingleton<InstanceManager>();
builder.Services.AddHostedService<CronTickService>();

// Blazor state (scoped per circuit)
builder.Services.AddScoped<AppState>();

// API client for Blazor → REST calls (in-process, uses loopback)
builder.Services.AddHttpClient("self", (sp, client) =>
{
    // Resolve the actual HTTP listen address from ASPNETCORE_URLS or config
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

// JWT Auth
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

// Blazor + API
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();

var app = builder.Build();

// Auto-migrate DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
    db.Database.EnsureCreated();
}

// Restore running instances
var instanceManager = app.Services.GetRequiredService<InstanceManager>();
await instanceManager.RestoreRunningInstancesAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseWebSockets();

// WebSocket endpoint for LocalAgent
app.Map("/ws/agent", async context =>
{
    var hub = context.RequestServices.GetRequiredService<WsHub>();
    await hub.HandleConnectionAsync(context);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseAntiforgery();
app.MapRazorComponents<QuantSaaS.SaaS.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
