namespace PublishGameCalendar.Domain;

public class Event
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}