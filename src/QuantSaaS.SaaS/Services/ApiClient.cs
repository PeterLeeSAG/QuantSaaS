using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QuantSaaS.SaaS.Models;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// HTTP client wrapper for calling the SaaS REST API from Blazor components.
/// Uses server-relative URLs since Blazor Server runs in-process.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AppState _appState;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http, AppState appState)
    {
        _http = http;
        _appState = appState;
    }

    private void SetAuth()
    {
        if (_appState.JwtToken != null)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _appState.JwtToken);
    }

    // ── Auth ──────────────────────────────────
    public async Task<(bool ok, string? token, string? email, Guid userId, string? error)> LoginAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (!resp.IsSuccessStatusCode)
            return (false, null, null, Guid.Empty, "Invalid credentials");

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        var token = doc!.RootElement.GetProperty("token").GetString()!;
        var userEmail = doc.RootElement.GetProperty("email").GetString()!;
        var userId = Guid.Parse(doc.RootElement.GetProperty("userId").GetString()!);
        return (true, token, userEmail, userId, null);
    }

    public async Task<(bool ok, string? error)> RegisterAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        if (!resp.IsSuccessStatusCode) return (false, "Registration failed");
        return (true, null);
    }

    // ── System ────────────────────────────────
    public async Task<SystemStatusResponse?> GetSystemStatusAsync()
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<SystemStatusResponse>("/api/v1/system/status", JsonOpts); }
        catch { return null; }
    }

    // ── Instances ─────────────────────────────
    public async Task<List<InstanceSummary>> GetInstancesAsync()
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<List<InstanceSummary>>("/api/v1/instances", JsonOpts) ?? new(); }
        catch { return new(); }
    }

    public async Task<(bool ok, Guid? instanceId)> CreateInstanceAsync(CreateInstanceRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("/api/v1/instances", req);
        if (!resp.IsSuccessStatusCode) return (false, null);
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        return (true, Guid.Parse(doc!.RootElement.GetProperty("instanceId").GetString()!));
    }

    public async Task<bool> PatchInstanceAsync(Guid id, string action)
    {
        SetAuth();
        var resp = await _http.PatchAsJsonAsync($"/api/v1/instances/{id}", new { action });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteInstanceAsync(Guid id)
    {
        SetAuth();
        var resp = await _http.DeleteAsync($"/api/v1/instances/{id}");
        return resp.IsSuccessStatusCode;
    }

    // ── Dashboard ─────────────────────────────
    public async Task<DashboardData?> GetDashboardDataAsync(Guid instanceId)
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<DashboardData>($"/api/v1/dashboard/data?instanceId={instanceId}", JsonOpts); }
        catch { return null; }
    }

    public async Task<List<EquityPoint>> GetEquitySnapshotsAsync(Guid instanceId, int days = 30)
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<List<EquityPoint>>($"/api/v1/dashboard/equity-snapshots?instanceId={instanceId}&days={days}", JsonOpts) ?? new(); }
        catch { return new(); }
    }

    // ── Evolution ─────────────────────────────
    public async Task<List<EvolutionTaskSummary>> GetEvolutionTasksAsync()
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<List<EvolutionTaskSummary>>("/api/v1/evolution/tasks", JsonOpts) ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> TriggerEvolutionAsync(TriggerEvolutionRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("/api/v1/evolution/tasks", req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<GenomeSummary>> GetGenomesAsync()
    {
        SetAuth();
        try { return await _http.GetFromJsonAsync<List<GenomeSummary>>("/api/v1/evolution/genomes", JsonOpts) ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> PromoteGenomeAsync(Guid genomeId)
    {
        SetAuth();
        var resp = await _http.PostAsync($"/api/v1/evolution/genomes/{genomeId}/promote", null);
        return resp.IsSuccessStatusCode;
    }
}
