using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly IUserService _users;

    public IndexModel(IUserService users) => _users = users;

    public SettingsViewModel Vm { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var userId = DbInitializer.SeedUserId;
        var settings = await _users.GetSettingsAsync(userId);

        Vm = new SettingsViewModel
        {
            Email             = settings?.Email ?? string.Empty,
            SubscriptionPlan  = settings?.SubscriptionPlan ?? "free",
            // Broker connection status is stored in the LocalAgent config (not in SaaS DB).
            // We expose placeholder entries until the Agent heartbeat API is implemented.
            AgentOnline  = false,
            AgentVersion = string.Empty,
            Brokers =
            [
                new BrokerStatus { Name = "okx",    DisplayName = "OKX Exchange",        Connected = false, AccountId = "" },
                new BrokerStatus { Name = "alpaca",  DisplayName = "Alpaca Markets",      Connected = false, AccountId = "" },
                new BrokerStatus { Name = "ibkr",    DisplayName = "Interactive Brokers", Connected = false, AccountId = "" },
            ]
        };
    }
}
