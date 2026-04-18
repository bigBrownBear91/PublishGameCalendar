namespace PublishGameCalendar.DTOs;

public class SeriesAdminDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string PollerType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public PollingConfigDto? PollingConfig { get; set; }
}