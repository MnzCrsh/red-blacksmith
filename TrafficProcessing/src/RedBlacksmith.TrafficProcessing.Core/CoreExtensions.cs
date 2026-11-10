using ClickHouse.Driver.Diagnostic;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedBlacksmith.TrafficProcessing.Application;
using RedBlacksmith.TrafficProcessing.Kafka;

namespace TrafficProcessing;

public static class CoreExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        var openTelemetryBuilder = services.AddOpenTelemetry()
                                           .ConfigureResource(builder => builder.AddService("traffic-processing"));
        
        openTelemetryBuilder.WithTracing(builder => builder.AddSource(TrafficProcessingTelemetry.InstrumentationName)
                                                           .AddSource(KafkaConsumerTelemetry.InstrumentationName)
                                                           .AddNpgsql()
                                                           .AddSource(ClickHouseDiagnosticsOptions.ActivitySourceName)
                                                           .AddOtlpExporter());
        
        openTelemetryBuilder.WithMetrics(builder => builder.AddMeter(TrafficProcessingTelemetry.InstrumentationName)
                                                           .AddMeter(KafkaConsumerTelemetry.InstrumentationName)
                                                           .AddMeter(nameof(Npgsql))
                                                           .AddRuntimeInstrumentation()
                                                           .AddOtlpExporter());
        
        return services;
    }
}
