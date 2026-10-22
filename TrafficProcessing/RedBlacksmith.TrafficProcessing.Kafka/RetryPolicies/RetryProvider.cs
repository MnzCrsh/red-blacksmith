using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace RedBlacksmith.TrafficProcessing.Kafka.RetryPolicies;

/// <summary>
/// Предоставляет доступ к Retry-пайплайнам.
/// </summary>
internal static class RetryProvider
{
    /// <summary>
    /// Создает Retry-пайплайн с экспоненциальной задержкой между повторами.
    /// </summary>
    /// <param name="logger">Класс для логгирования провалившихся попыток.</param>
    /// <param name="maxRetryAttempts">Максимальное количество повторов.</param>
    /// <param name="defaultRetryDelayMs">Базовая задержка между повторами.</param>
    /// <param name="maxRetryDelayMs">Максимальный размер до которого может вырасти задержка.</param>
    public static ResiliencePipeline CreateRetryPipeline(ILogger<KafkaConsumerWorker> logger,
        int maxRetryAttempts,
        int defaultRetryDelayMs,
        int maxRetryDelayMs) =>
        new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(defaultRetryDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(maxRetryDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            
            OnRetry = args =>
            {
                logger.LogWarning(args.Outcome.Exception,
                                  "Kafka operation failed. Retry {RetryAttempt}/{MaxRetries} in {DelayMs} ms.",
                                  args.AttemptNumber + 1,
                                  maxRetryAttempts,
                                  args.RetryDelay.TotalMilliseconds);
                
                return ValueTask.CompletedTask;
            }
        }).Build();
}