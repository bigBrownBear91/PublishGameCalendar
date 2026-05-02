namespace PublishGameCalendar.DTOs;

public class SeriesDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastChangeAt { get; set; }
}