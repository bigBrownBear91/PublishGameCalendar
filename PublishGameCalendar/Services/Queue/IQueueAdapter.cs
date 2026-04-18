using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Queue;

public interface IQueueAdapter
{
    Task PublishAsync(NotificationMessage message);
}