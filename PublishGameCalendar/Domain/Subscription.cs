using PublishGameCalendar.Identity;

namespace PublishGameCalendar.Domain;

public class Subscription
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;
}