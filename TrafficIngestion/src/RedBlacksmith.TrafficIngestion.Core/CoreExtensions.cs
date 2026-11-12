using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedBlacksmith.TrafficIngestion.Application;

namespace RedBlacksmith;

public static class CoreExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        var openTelemetryBuilder = services.AddOpenTelemetry()
                                           .ConfigureResource(builder => builder.AddService("traffic-ingestion"));

        openTelemetryBuilder.WithTracing(tracing => tracing.AddSource(TrafficIngestionTelemetry.InstrumentationName)
                                                           .AddAspNetCoreInstrumentation()
                                                           .AddOtlpExporter());
        
        openTelemetryBuilder.WithMetrics(metrics => metrics.AddMeter(TrafficIngestionTelemetry.InstrumentationName)
                                                           .AddRuntimeInstrumentation()
                                                           .AddAspNetCoreInstrumentation()
                                                           .AddOtlpExporter());
        
        return services;
    }
}