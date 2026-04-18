namespace PublishGameCalendar.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> SubscribedSeries { get; set; } = new List<string>();
}