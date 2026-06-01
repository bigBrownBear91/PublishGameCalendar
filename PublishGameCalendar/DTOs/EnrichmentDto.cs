namespace PublishGameCalendar.DTOs;

public class EnrichmentDto
{
    public string EventUid { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}
