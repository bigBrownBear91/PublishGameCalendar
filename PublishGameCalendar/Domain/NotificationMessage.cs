namespace PublishGameCalendar.Domain;

public class NotificationMessage
{
    public string SeriesName { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public List<string> RecipientEmails { get; set; } = new List<string>();
}