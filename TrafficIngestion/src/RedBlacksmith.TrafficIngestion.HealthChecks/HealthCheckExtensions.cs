using Microsoft.Extensions.DependencyInjection;

namespace RedBlacksmith.HealthChecks;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddIngestionHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
                .AddCheck<KafkaReadinessHealthCheck>("kafka", tags: ["ready"])
                .AddCheck<ChannelWritableHealthCheck>("channel", tags: ["ready"]);
        
        return services;
    }
}