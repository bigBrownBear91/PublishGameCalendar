using Amazon.DynamoDBv2.DataModel;

namespace PublishGameCalendar.Domain;

[DynamoDBTable("series")]
public class Series
{
    [DynamoDBHashKey("id")]
    public string Id { get; set; } = string.Empty;

    [DynamoDBProperty("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDBProperty("source_url")]
    public string SourceUrl { get; set; } = string.Empty;

    [DynamoDBProperty("poller_type")]
    public string PollerType { get; set; } = string.Empty;

    [DynamoDBProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [DynamoDBProperty("created_at")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");

    [DynamoDBIgnore]
    public PollingConfig? PollingConfig { get; set; }
}
