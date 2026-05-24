using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using QuantSaaS.Agent;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("config.agent.json", optional: true);
        config.AddEnvironmentVariables("QUANTSAAS_AGENT_");
    })
    .ConfigureServices((context, services) =>
    {
        var agentConfig = context.Configuration.GetSection("Agent").Get<AgentConfig>()
            ?? new AgentConfig
            {
                SaaSUrl = "http://localhost:5000",
                Email = "agent@test.com",
                Password = "changeme",
                Exchange = new ExchangeConfig { Sandbox = true }
            };

        services.AddSingleton(agentConfig);
        services.AddSingleton(agentConfig.Exchange);
        services.AddSingleton<BitgetExchange>();
        services.AddHostedService<AgentWsClient>();
    })
    .Build();

await host.RunAsync();
