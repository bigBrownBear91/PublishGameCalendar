using PublishGameCalendar.Identity;

namespace PublishGameCalendar.Domain;

public class Subscription
{
    public string UserId { get; set; } = string.Empty;
    // ReSharper disable once NullableWarningSuppressionIsUsed
    public ApplicationUser User { get; set; } = null!;
    public int SeriesId { get; set; }
    // ReSharper disable once NullableWarningSuppressionIsUsed
    public Series Series { get; set; } = null!;
}