namespace PublishGameCalendar.Domain;

public class Series
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string PollerType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PollingConfig? PollingConfig { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}