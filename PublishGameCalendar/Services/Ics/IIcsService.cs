using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Ics;

public interface IIcsService
{
    Task<List<Event>> ParseAsync(int seriesId);
    Task<EventDiff> DiffAsync(int seriesId, List<Event> freshEvents);
    Task WriteAsync(int seriesId, List<Event> events);
    string GetIcsFilePath(int seriesId);
}