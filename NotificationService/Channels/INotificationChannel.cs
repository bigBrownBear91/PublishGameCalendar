using NotificationService.Domain;

namespace NotificationService.Channels;

public interface INotificationChannel
{
    Task SendAsync(NotificationMessage message);
}