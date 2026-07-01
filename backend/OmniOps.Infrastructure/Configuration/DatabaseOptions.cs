namespace OmniOps.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5433;
    public string Database { get; set; } = "OmniOps";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    public string ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
    }
}
