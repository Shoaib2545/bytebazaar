using ByteBazaar.Application.Abstractions;

namespace ByteBazaar.Tests;

public class FakeNotificationQueue : IOrderNotificationQueue
{
    public List<(string OrderNumber, string Email)> Placed { get; } = new();
    public List<(string OrderNumber, string Status, string Email)> StatusChanges { get; } = new();

    public Task EnqueueOrderPlacedAsync(string orderNumber, string email)
    {
        Placed.Add((orderNumber, email));
        return Task.CompletedTask;
    }

    public Task EnqueueStatusChangedAsync(string orderNumber, string status, string email)
    {
        StatusChanges.Add((orderNumber, status, email));
        return Task.CompletedTask;
    }
}

public class FakeRevalidator : IStorefrontRevalidator
{
    public List<string> Paths { get; } = new();

    public void Revalidate(params string[] paths) => Paths.AddRange(paths);
}
