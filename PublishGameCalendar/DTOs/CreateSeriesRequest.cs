using System.ComponentModel.DataAnnotations;

namespace PublishGameCalendar.DTOs;

public class CreateSeriesRequest
{
    [Required] public string Name { get; set; } = string.Empty;

    [Required] [Url] public string SourceUrl { get; set; } = string.Empty;

    [Required] public string PollerType { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [Range(1, 8760)] public int IntervalHours { get; set; } = 1;
}