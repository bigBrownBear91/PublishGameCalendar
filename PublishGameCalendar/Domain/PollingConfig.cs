namespace PublishGameCalendar.Domain;

public class PollingConfig
{
    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;
    public int IntervalHours { get; set; } = 1;
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastChangeAt { get; set; }
    public bool Enabled { get; set; } = true;
}