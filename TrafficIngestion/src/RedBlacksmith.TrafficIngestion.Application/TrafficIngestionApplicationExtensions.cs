using Microsoft.Extensions.DependencyInjection;

namespace RedBlacksmith.TrafficIngestion.Application;

public static class TrafficIngestionApplicationExtensions
{
    public static IServiceCollection AddTrafficIngestionApplication(this IServiceCollection services)
    {
        services.AddSingleton<KafkaEventProducer>()
                .AddHostedService<KafkaPublishWorker>();
        
        return services;
    }
}