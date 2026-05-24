using QuantSaaS.SaaS.Models;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// Scoped Blazor application state — holds auth token and current user info.
/// </summary>
public class AppState
{
    public string? JwtToken { get; private set; }
    public string? UserEmail { get; private set; }
    public Guid? UserId { get; private set; }
    public bool IsAuthenticated => JwtToken != null;

    public event Action? OnChange;

    public void SetAuth(string token, string email, Guid userId)
    {
        JwtToken = token;
        UserEmail = email;
        UserId = userId;
        NotifyStateChanged();
    }

    public void ClearAuth()
    {
        JwtToken = null;
        UserEmail = null;
        UserId = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
