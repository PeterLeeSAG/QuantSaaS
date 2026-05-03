namespace QuantSaaS.Core.Config;

public class AppConfig
{
    public string Role { get; set; } = "dev"; // saas | lab | dev
    public DatabaseConfig Database { get; set; } = new();
    public RedisConfig Redis { get; set; } = new();
    public JwtConfig Jwt { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class RedisConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";
}

public class JwtConfig
{
    public string Secret { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 24;
}

public class ServerConfig
{
    public int Port { get; set; } = 5000;
}
