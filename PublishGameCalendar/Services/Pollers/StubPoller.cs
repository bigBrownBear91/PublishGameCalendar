using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Pollers;

public class StubPoller : IWebsitePoller
{
    public Task<List<Event>> FetchEventsAsync(Series series)
    {
        DateTime start = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        return Task.FromResult(new List<Event>
        {
            new Event { Uid = "stub-1", Title = "Opponent A", Start = start, End = start.AddHours(2) }
        });
    }
}
