using ByteBazaar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ByteBazaar.Infrastructure.Notifications;

/// <summary>
/// Development stand-in for transactional email: order notifications are written to the log.
/// </summary>
public class SerilogOrderNotifier : IOrderNotifier
{
    private readonly ILogger<SerilogOrderNotifier> _logger;

    public SerilogOrderNotifier(ILogger<SerilogOrderNotifier> logger)
    {
        _logger = logger;
    }

    public Task OrderPlacedAsync(string orderNumber, string email, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Notification] Order {OrderNumber} placed — confirmation email would be sent to {Email}.",
            orderNumber, email);
        return Task.CompletedTask;
    }

    public Task OrderStatusChangedAsync(string orderNumber, string status, string email, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Notification] Order {OrderNumber} status changed to {Status} — update email would be sent to {Email}.",
            orderNumber, status, email);
        return Task.CompletedTask;
    }
}
