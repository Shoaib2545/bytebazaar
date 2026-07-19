using ByteBazaar.Application.Abstractions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ByteBazaar.Infrastructure.Notifications;

/// <summary>
/// Enqueues notifications on Hangfire when it is available (DB reachable at startup);
/// otherwise calls the notifier inline. Never throws — notifications are best-effort.
/// </summary>
public class OrderNotificationQueue : IOrderNotificationQueue
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderNotificationQueue> _logger;

    public OrderNotificationQueue(IServiceProvider serviceProvider, ILogger<OrderNotificationQueue> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task EnqueueOrderPlacedAsync(string orderNumber, string email)
        => DispatchAsync(
            client => client.Enqueue<IOrderNotifier>(n => n.OrderPlacedAsync(orderNumber, email, CancellationToken.None)),
            notifier => notifier.OrderPlacedAsync(orderNumber, email));

    public Task EnqueueStatusChangedAsync(string orderNumber, string status, string email)
        => DispatchAsync(
            client => client.Enqueue<IOrderNotifier>(n => n.OrderStatusChangedAsync(orderNumber, status, email, CancellationToken.None)),
            notifier => notifier.OrderStatusChangedAsync(orderNumber, status, email));

    private async Task DispatchAsync(Action<IBackgroundJobClient> enqueue, Func<IOrderNotifier, Task> inline)
    {
        try
        {
            var client = _serviceProvider.GetService<IBackgroundJobClient>();
            if (client is not null)
            {
                enqueue(client);
                return;
            }

            await inline(_serviceProvider.GetRequiredService<IOrderNotifier>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch order notification; continuing without it.");
        }
    }
}
