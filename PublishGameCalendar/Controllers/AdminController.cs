using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublishGameCalendar.Domain;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IPollingConfigRepository _pollingConfigRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IEnrichmentRepository _enrichmentRepo;
    private readonly IIcsService _icsService;

    public AdminController(
        ISeriesRepository seriesRepo,
        IPollingConfigRepository pollingConfigRepo,
        IEnrichmentRepository enrichmentRepo,
        IIcsService icsService)
    {
        _seriesRepo = seriesRepo;
        _pollingConfigRepo = pollingConfigRepo;
        _enrichmentRepo = enrichmentRepo;
        _icsService = icsService;
    }

    // ── Series ──

    [HttpGet("series")]
    public async Task<ActionResult<List<SeriesAdminDto>>> GetSeries()
    {
        List<Series> series = await _seriesRepo.GetAllAsync();
        List<SeriesAdminDto> dtos = series.Select(s => new SeriesAdminDto
        {
            Id = s.Id,
            Name = s.Name,
            SourceUrl = s.SourceUrl,
            PollerType = s.PollerType,
            Enabled = s.Enabled,
            CreatedAt = DateTime.Parse(s.CreatedAt),
            PollingConfig = s.PollingConfig is null
                ? null
                : new PollingConfigDto
                {
                    SeriesId = s.PollingConfig.SeriesId,
                    SeriesName = s.Name,
                    IntervalHours = s.PollingConfig.IntervalHours,
                    LastPolledAt = s.PollingConfig.LastPolledAt,
                    LastChangeAt = s.PollingConfig.LastChangeAt,
                    LastPollFailed = s.PollingConfig.LastPollFailed,
                    Enabled = s.PollingConfig.Enabled
                }
        }).ToList();
        return Ok(dtos);
    }

    [HttpPost("series")]
    // TODO: validate that request.PollerType is registered in PollerFactory before saving
    public async Task<IActionResult> CreateSeries([FromBody] CreateSeriesRequest request)
    {
        Series series = new Series
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            SourceUrl = request.SourceUrl,
            PollerType = request.PollerType,
            Enabled = request.Enabled
        };
        await _seriesRepo.CreateAsync(series);

        PollingConfig config = new PollingConfig { SeriesId = series.Id, IntervalHours = request.IntervalHours };
        await _pollingConfigRepo.CreateAsync(config);

        return NoContent();
    }

    [HttpPut("series/{id}")]
    public async Task<IActionResult> UpdateSeries(string id, [FromBody] UpdateSeriesRequest request)
    {
        Series? series = await _seriesRepo.GetByIdAsync(id);
        if (series is null) return NotFound();

        series.Name = request.Name;
        series.SourceUrl = request.SourceUrl;
        series.PollerType = request.PollerType;
        series.Enabled = request.Enabled;
        await _seriesRepo.UpdateAsync(series);
        return NoContent();
    }

    [HttpDelete("series/{id}")]
    public async Task<IActionResult> DeleteSeries(string id)
    {
        if (await _seriesRepo.GetByIdAsync(id) is null) return NotFound();
        await _enrichmentRepo.DeleteAllBySeriesIdAsync(id);
        await _icsService.DeleteFilesAsync(id);
        await _seriesRepo.DeleteAsync(id);
        return NoContent();
    }

    // ── Polling Config ──

    [HttpGet("polling-config")]
    public async Task<ActionResult<List<PollingConfigDto>>> GetPollingConfigs()
    {
        List<PollingConfig> configs = await _pollingConfigRepo.GetAllAsync();
        List<PollingConfigDto> dtos = configs.Select(c => new PollingConfigDto
        {
            SeriesId = c.SeriesId,
            SeriesName = c.Series.Name,
            IntervalHours = c.IntervalHours,
            LastPolledAt = c.LastPolledAt,
            LastChangeAt = c.LastChangeAt,
            LastPollFailed = c.LastPollFailed,
            LastEventCount = c.LastEventCount,
            Enabled = c.Enabled
        }).ToList();

        return Ok(dtos);
    }

    [HttpPut("polling-config/{seriesId}")]
    public async Task<IActionResult> UpdatePollingConfig(string seriesId, [FromBody] UpdatePollingConfigRequest request)
    {
        PollingConfig? config = await _pollingConfigRepo.GetBySeriesIdAsync(seriesId);
        if (config is null) return NotFound();

        config.IntervalHours = request.IntervalHours;
        config.Enabled = request.Enabled;
        await _pollingConfigRepo.UpdateAsync(config);
        return NoContent();
    }
}
