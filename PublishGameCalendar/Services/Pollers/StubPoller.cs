using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Pollers;

/// <summary>
///     Placeholder poller that returns an empty event list. Replace with a concrete implementation per source website.
/// </summary>
public class StubPoller : IWebsitePoller
{
    public Task<List<Event>> FetchEventsAsync(Series series)
    {
        return Task.FromResult(new List<Event>());
    }
}