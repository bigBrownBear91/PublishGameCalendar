using System.ComponentModel.DataAnnotations;

namespace PublishGameCalendar.DTOs;

public class UpsertEnrichmentRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }
}
