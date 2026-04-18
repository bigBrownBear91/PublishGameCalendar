namespace PublishGameCalendar.Domain;

public class EventDiff
{
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Modified.Count > 0;
    public List<Event> Added { get; set; } = new List<Event>();
    public List<Event> Removed { get; set; } = new List<Event>();
    public List<Event> Modified { get; set; } = new List<Event>();

    public string BuildSummary()
    {
        List<string> parts = new List<string>();
        if (Added.Count > 0) parts.Add($"{Added.Count} added");
        if (Removed.Count > 0) parts.Add($"{Removed.Count} removed");
        if (Modified.Count > 0) parts.Add($"{Modified.Count} modified");
        return string.Join(", ", parts);
    }
}