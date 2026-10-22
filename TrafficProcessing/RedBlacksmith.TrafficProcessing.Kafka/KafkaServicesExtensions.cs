using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedBlacksmith.TrafficProcessing.Kafka;

public static class KafkaServicesExtensions
{
    public static IServiceCollection AddKafkaServices(this IServiceCollection services)
    {

        services.AddSingleton(AddProducer)
                .AddSingleton(AddConsumerConfig);
        
        services.AddSingleton<KafkaDeadLetterPublisher>()
                .AddHostedService<KafkaConsumerWorker>();
        
        return services;
    }

    private static ConsumerConfig AddConsumerConfig(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IConfiguration>();

        return new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            GroupId = config["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            MaxPollIntervalMs = int.TryParse(config["Kafka:MaxPollIntervalMs"], out var value) ? value : 5000,
        };
    }

    private static IProducer<Null, byte[]> AddProducer(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = 5,
            CompressionType = CompressionType.Lz4
        };
                    
                    
        return new ProducerBuilder<Null, byte[]>(producerConfig)
               .SetKeySerializer(Serializers.Null)
               .SetValueSerializer(Serializers.ByteArray)
               .Build();
    }
}