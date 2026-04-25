namespace PublishGameCalendar.DTOs;

public class PollingConfigDto
{
    public int SeriesId { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public int IntervalHours { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastChangeAt { get; set; }
    public bool LastPollFailed { get; set; }
    public int? LastEventCount { get; set; }
    public bool Enabled { get; set; }
}