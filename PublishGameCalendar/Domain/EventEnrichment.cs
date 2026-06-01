using Amazon.DynamoDBv2.DataModel;

namespace PublishGameCalendar.Domain;

[DynamoDBTable("event_enrichments")]
public class EventEnrichment
{
    [DynamoDBHashKey("series_id")]
    public string SeriesId { get; set; } = string.Empty;

    [DynamoDBRangeKey("event_uid")]
    public string EventUid { get; set; } = string.Empty;

    [DynamoDBProperty("title")]
    public string? Title { get; set; }

    [DynamoDBProperty("location")]
    public string? Location { get; set; }

    [DynamoDBProperty("description")]
    public string? Description { get; set; }
}
