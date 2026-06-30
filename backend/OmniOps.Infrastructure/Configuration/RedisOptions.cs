namespace OmniOps.Infrastructure.Configuration
{
    public class RedisOptions
    {
        public const string SectionName = "Redis";
        public string Configuration { get; set; } = "127.0.0.1:6379";
        public string InstanceName { get; set; } = "OmniOps_";
    }
}
