using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Settings;

public class IndexModel : PageModel
{
    public SettingsViewModel Vm { get; private set; } = new();

    public void OnGet()
    {
        Vm = new SettingsViewModel
        {
            Email = "trader@example.com",
            SubscriptionPlan = "pro",
            AgentOnline = true,
            AgentVersion = "1.2.0",
            Brokers =
            [
                new BrokerStatus { Name = "okx",    DisplayName = "OKX Exchange",     Connected = true,  AccountId = "okx-***-4821" },
                new BrokerStatus { Name = "alpaca",  DisplayName = "Alpaca Markets",   Connected = true,  AccountId = "PA-***-9037" },
                new BrokerStatus { Name = "ibkr",    DisplayName = "Interactive Brokers", Connected = false, AccountId = "" },
            ]
        };
    }
}
