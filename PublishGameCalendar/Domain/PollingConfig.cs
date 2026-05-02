using Amazon.DynamoDBv2.DataModel;

namespace PublishGameCalendar.Domain;

[DynamoDBTable("polling_config")]
public class PollingConfig
{
    [DynamoDBHashKey("series_id")]
    public string SeriesId { get; set; } = string.Empty;

    [DynamoDBIgnore]
    public Series Series { get; set; } = null!;

    [DynamoDBProperty("interval_hours")]
    public int IntervalHours { get; set; } = 1;

    [DynamoDBProperty("last_polled_at")]
    public DateTime? LastPolledAt { get; set; }

    [DynamoDBProperty("last_change_at")]
    public DateTime? LastChangeAt { get; set; }

    [DynamoDBProperty("last_poll_failed")]
    public bool LastPollFailed { get; set; }

    [DynamoDBProperty("last_event_count")]
    public int? LastEventCount { get; set; }

    [DynamoDBProperty("enabled")]
    public bool Enabled { get; set; } = true;
}
