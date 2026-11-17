using Microsoft.Extensions.Hosting;

namespace RedBlacksmith.EndToEnd;

public abstract class IntegrationTestBase(TestContainersFixture infrastructure)
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Общая инфраструктура интеграционных тестов.
    /// </summary>
    protected TestContainersFixture Infrastructure { get; } = infrastructure;
    
    /// <summary>
    /// Запускает хост с ограничением времени.
    /// </summary>
    /// <param name="host">Запускаемый хост.</param>
    /// <param name="serviceName">Название сервиса для сообщения об ошибке.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    protected static async Task StartHostAsync(IHost host, string serviceName, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(StartupTimeout);

        try
        {
            await host.StartAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Сервис {serviceName} не запустился за {StartupTimeout}.", exception);
        }
    }
}
