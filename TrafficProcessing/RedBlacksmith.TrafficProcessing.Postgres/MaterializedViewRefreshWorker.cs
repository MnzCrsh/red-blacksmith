using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RedBlacksmith.TrafficProcessing.Postgres;

public class MaterializedViewRefreshWorker(
    MaterializedViewRefreshService refreshService,
    ILogger<MaterializedViewRefreshWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15)
    ];
    
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            await RefreshWithRetryAsync(stoppingToken);

        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Обновляет представления с ограниченным количеством повторных попыток.
    /// </summary>
    private async Task RefreshWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await refreshService.RefreshSourceTrafficMinuteAsync(cancellationToken);

                logger.LogDebug(
                                "Materialized views refreshed successfully.");

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogError(exception, "Failed to refresh materialized views after {MaxAttempts} attempts.", MaxAttempts);
                    return;
                }

                var retryDelay = RetryDelays[attempt - 1];

                logger.LogWarning(exception, "Failed to refresh materialized views. Attempt {Attempt}/{MaxAttempts}. Retry in {RetryDelay}.", attempt, MaxAttempts, retryDelay);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }
}
