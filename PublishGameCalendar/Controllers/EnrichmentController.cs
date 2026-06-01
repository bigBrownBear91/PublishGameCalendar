using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublishGameCalendar.Domain;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Enrichment;
using PublishGameCalendar.Services.Ics;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/enrichments")]
[Authorize(Roles = "Admin")]
public class EnrichmentController : ControllerBase
{
    private readonly IEnrichmentRepository _enrichmentRepo;
    private readonly IIcsService _icsService;
    private readonly IEventEnricher _eventEnricher;
    private readonly ISeriesRepository _seriesRepo;

    public EnrichmentController(
        IEnrichmentRepository enrichmentRepo,
        IIcsService icsService,
        IEventEnricher eventEnricher,
        ISeriesRepository seriesRepo)
    {
        _enrichmentRepo = enrichmentRepo;
        _icsService = icsService;
        _eventEnricher = eventEnricher;
        _seriesRepo = seriesRepo;
    }

    [HttpGet("{seriesId}")]
    public async Task<ActionResult<List<EnrichmentDto>>> GetEnrichments(string seriesId)
    {
        if (await _seriesRepo.GetByIdAsync(seriesId) is null)
            return NotFound();

        List<EventEnrichment> enrichments = await _enrichmentRepo.GetBySeriesIdAsync(seriesId);
        List<EnrichmentDto> dtos = enrichments.Select(e => new EnrichmentDto
        {
            EventUid = e.EventUid,
            Title = e.Title,
            Location = e.Location,
            Description = e.Description
        }).ToList();

        return Ok(dtos);
    }

    [HttpPut("{seriesId}/{eventUid}")]
    public async Task<IActionResult> UpsertEnrichment(
        string seriesId,
        string eventUid,
        [FromBody] UpsertEnrichmentRequest request)
    {
        Series? series = await _seriesRepo.GetByIdAsync(seriesId);
        if (series is null)
            return NotFound();

        EventEnrichment enrichment = new EventEnrichment
        {
            SeriesId = seriesId,
            EventUid = eventUid,
            Title = request.Title,
            Location = request.Location,
            Description = request.Description
        };
        await _enrichmentRepo.UpsertAsync(enrichment);

        await RegenerateEnrichedIcsAsync(seriesId, series.Name);
        return NoContent();
    }

    [HttpDelete("{seriesId}/{eventUid}")]
    public async Task<IActionResult> DeleteEnrichment(string seriesId, string eventUid)
    {
        Series? series = await _seriesRepo.GetByIdAsync(seriesId);
        if (series is null)
            return NotFound();

        await _enrichmentRepo.DeleteAsync(seriesId, eventUid);

        await RegenerateEnrichedIcsAsync(seriesId, series.Name);
        return NoContent();
    }

    private async Task RegenerateEnrichedIcsAsync(string seriesId, string seriesName)
    {
        List<Event> rawEvents = await _icsService.ParseRawSnapshotAsync(seriesId);
        if (rawEvents.Count == 0)
            return;

        List<EventEnrichment> enrichments = await _enrichmentRepo.GetBySeriesIdAsync(seriesId);
        List<Event> enrichedEvents = _eventEnricher.Merge(rawEvents, enrichments);
        await _icsService.WriteAsync(seriesId, seriesName, enrichedEvents);
    }
}
