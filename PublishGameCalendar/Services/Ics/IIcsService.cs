using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Ics;

public interface IIcsService
{
    Task<List<Event>> ParseAsync(string seriesId);
    Task<EventDiff> DiffAsync(string seriesId, List<Event> freshEvents);
    Task WriteAsync(string seriesId, string seriesName, List<Event> events);
    string GetIcsFilePath(string seriesId);

    Task<List<Event>> ParseRawSnapshotAsync(string seriesId);
    Task WriteRawSnapshotAsync(string seriesId, List<Event> events);
    Task<EventDiff> DiffRawAsync(string seriesId, List<Event> freshEvents);
    Task DeleteFilesAsync(string seriesId);
}
