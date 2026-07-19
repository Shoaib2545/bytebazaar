namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// Sends order notifications (dev stand-in logs via Serilog; production would send email/SMS).
/// </summary>
public interface IOrderNotifier
{
    Task OrderPlacedAsync(string orderNumber, string email, CancellationToken cancellationToken = default);
    Task OrderStatusChangedAsync(string orderNumber, string status, string email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enqueues order notifications on a background queue (Hangfire) when available,
/// otherwise invokes <see cref="IOrderNotifier"/> inline. Never throws.
/// </summary>
public interface IOrderNotificationQueue
{
    Task EnqueueOrderPlacedAsync(string orderNumber, string email);
    Task EnqueueStatusChangedAsync(string orderNumber, string status, string email);
}
