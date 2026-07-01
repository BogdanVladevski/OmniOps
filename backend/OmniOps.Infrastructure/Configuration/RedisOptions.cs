namespace OmniOps.Infrastructure.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6379;
    public string ConnectionString { get; set; } = "127.0.0.1:6379,abortConnect=false";
    public string InstanceName { get; set; } = "OmniOps_";

    public string ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        return $"{Host}:{Port},abortConnect=false";
    }
}
