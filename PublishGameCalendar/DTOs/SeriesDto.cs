namespace PublishGameCalendar.DTOs;

public class SeriesDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastChangeAt { get; set; }
}