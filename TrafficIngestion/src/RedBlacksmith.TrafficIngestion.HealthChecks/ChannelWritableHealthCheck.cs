using System.Threading.Channels;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RedBlacksmith.TrafficIngestion.Models;

namespace RedBlacksmith.HealthChecks;

public class ChannelWritableHealthCheck(ChannelWriter<PacketEvent> channelWriter) : IHealthCheck
{
    private static readonly TimeSpan WriteWaitTimeout = TimeSpan.FromMilliseconds(100);
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WriteWaitTimeout);
        try
        {
            var canWrite = await channelWriter.WaitToWriteAsync(timeoutCts.Token);

            return canWrite 
                ? HealthCheckResult.Healthy() 
                : HealthCheckResult.Unhealthy("Входной канал событий закрыт.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Входной буфер событий переполнен.");
        }
    }
}
