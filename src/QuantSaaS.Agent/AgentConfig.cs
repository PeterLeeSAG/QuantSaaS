namespace QuantSaaS.Agent;

public class AgentConfig
{
    public string SaaSUrl { get; set; } = "http://localhost:5000";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public ExchangeConfig Exchange { get; set; } = new();
}

public class ExchangeConfig
{
    public string Name { get; set; } = "Bitget";
    public string ApiKey { get; set; } = string.Empty; // Only here, never in SaaS
    public string SecretKey { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;
    public bool Sandbox { get; set; } = true;
}
