using NotificationService.Channels;
using NotificationService.Domain;

namespace NotificationService;

public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationChannel> _channels;

    public NotificationDispatcher(IEnumerable<INotificationChannel> channels)
    {
        _channels = channels;
    }

    public async Task DispatchAsync(NotificationMessage message)
    {
        foreach (INotificationChannel channel in _channels)
            await channel.SendAsync(message);
    }
}