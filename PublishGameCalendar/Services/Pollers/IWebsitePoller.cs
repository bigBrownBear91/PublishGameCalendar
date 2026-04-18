using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Pollers;

public interface IWebsitePoller
{
    Task<List<Event>> FetchEventsAsync(Series series);
}