using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Ics;

public class IcsService : IIcsService
{
    private readonly string _icsFilesPath;

    public IcsService(IConfiguration config)
    {
        _icsFilesPath = config["IcsFilesPath"] ?? "/data/ics";
        Directory.CreateDirectory(_icsFilesPath);
    }

    public string GetIcsFilePath(string seriesId) =>
        Path.Combine(_icsFilesPath, $"{seriesId}.ics");

    private string GetRawSnapshotFilePath(string seriesId) =>
        Path.Combine(_icsFilesPath, $"{seriesId}_raw.ics");

    public Task<List<Event>> ParseAsync(string seriesId) =>
        ParseFileAsync(GetIcsFilePath(seriesId));

    public Task<List<Event>> ParseRawSnapshotAsync(string seriesId) =>
        ParseFileAsync(GetRawSnapshotFilePath(seriesId));

    private static Task<List<Event>> ParseFileAsync(string path)
    {
        if (!File.Exists(path))
            return Task.FromResult(new List<Event>());

        string content = File.ReadAllText(path);
        Calendar? calendar = Calendar.Load(content);
        List<Event> events = calendar.Events.Select(MapToEvent).ToList();
        return Task.FromResult(events);
    }

    public async Task<EventDiff> DiffAsync(string seriesId, List<Event> freshEvents)
    {
        List<Event> existing = await ParseAsync(seriesId);
        return ComputeDiff(existing, freshEvents);
    }

    public async Task<EventDiff> DiffRawAsync(string seriesId, List<Event> freshEvents)
    {
        List<Event> existing = await ParseRawSnapshotAsync(seriesId);
        return ComputeDiff(existing, freshEvents);
    }

    private static EventDiff ComputeDiff(List<Event> existing, List<Event> freshEvents)
    {
        Dictionary<string, Event> existingById = existing.ToDictionary(e => e.Uid);
        Dictionary<string, Event> freshById = freshEvents.ToDictionary(e => e.Uid);

        EventDiff diff = new EventDiff();

        foreach (Event fresh in freshEvents)
            if (!existingById.TryGetValue(fresh.Uid, out Event? old))
                diff.Added.Add(fresh);
            else if (!EventsAreEqual(old, fresh))
                diff.Modified.Add(fresh);

        foreach (Event old in existing)
            if (!freshById.ContainsKey(old.Uid))
                diff.Removed.Add(old);

        return diff;
    }

    public Task WriteAsync(string seriesId, string seriesName, List<Event> events)
    {
        Calendar calendar = new Calendar();
        calendar.AddProperty("X-WR-CALNAME", seriesName);
        foreach (Event ev in events)
            calendar.Events.Add(MapToCalendarEvent(ev));

        CalendarSerializer serializer = new CalendarSerializer();
        string? content = serializer.SerializeToString(calendar);
        File.WriteAllText(GetIcsFilePath(seriesId), content);
        return Task.CompletedTask;
    }

    public Task WriteRawSnapshotAsync(string seriesId, List<Event> events)
    {
        Calendar calendar = new Calendar();
        foreach (Event ev in events)
            calendar.Events.Add(MapToCalendarEvent(ev));

        CalendarSerializer serializer = new CalendarSerializer();
        string? content = serializer.SerializeToString(calendar);
        File.WriteAllText(GetRawSnapshotFilePath(seriesId), content);
        return Task.CompletedTask;
    }

    public Task DeleteFilesAsync(string seriesId)
    {
        string icsPath = GetIcsFilePath(seriesId);
        string rawPath = GetRawSnapshotFilePath(seriesId);
        if (File.Exists(icsPath)) File.Delete(icsPath);
        if (File.Exists(rawPath)) File.Delete(rawPath);
        return Task.CompletedTask;
    }

    private static Event MapToEvent(CalendarEvent ce)
    {
        return new Event
        {
            Uid = ce.Uid,
            Title = ce.Summary,
            Start = ce.DtStart.AsUtc,
            End = ce.DtEnd.AsUtc,
            Location = ce.Location,
            Description = ce.Description
        };
    }

    private static CalendarEvent MapToCalendarEvent(Event ev)
    {
        return new CalendarEvent
        {
            Uid = ev.Uid,
            Summary = ev.Title,
            DtStart = new CalDateTime(ev.Start.ToUniversalTime()),
            DtEnd = new CalDateTime(ev.End.ToUniversalTime()),
            Location = ev.Location,
            Description = ev.Description
        };
    }

    private static bool EventsAreEqual(Event a, Event b)
    {
        return a.Title == b.Title &&
               a.Start == b.Start &&
               a.End == b.End &&
               a.Location == b.Location &&
               a.Description == b.Description;
    }
}
