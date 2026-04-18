using Microsoft.AspNetCore.Mvc;
using PublishGameCalendar.Domain;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/series")]
public class SeriesController : ControllerBase
{
    private readonly IIcsService _icsService;
    private readonly ISeriesRepository _seriesRepo;

    public SeriesController(ISeriesRepository seriesRepo, IIcsService icsService)
    {
        _seriesRepo = seriesRepo;
        _icsService = icsService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SeriesDto>>> GetAll()
    {
        List<Series> series = await _seriesRepo.GetAllAsync();
        string baseUrl = $"{Request.Scheme}://{Request.Host}";
        List<SeriesDto> dtos = series.Select(s => new SeriesDto
        {
            Id = s.Id,
            Name = s.Name,
            IcsUrl = $"{baseUrl}/api/series/{s.Id}/calendar.ics",
            Enabled = s.Enabled,
            LastPolledAt = s.PollingConfig?.LastPolledAt,
            LastChangeAt = s.PollingConfig?.LastChangeAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}/calendar.ics")]
    public async Task<IActionResult> GetIcsFile(int id)
    {
        Series? series = await _seriesRepo.GetByIdAsync(id);
        if (series is null) return NotFound();

        string path = _icsService.GetIcsFilePath(id);
        if (!System.IO.File.Exists(path))
            return NotFound("No calendar file available yet for this series.");

        return PhysicalFile(path, "text/calendar", $"{series.Name}.ics");
    }
}