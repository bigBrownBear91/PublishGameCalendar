using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PublishGameCalendar.Domain;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Identity;
using PublishGameCalendar.Repositories;

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IPollingConfigRepository _pollingConfigRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ISeriesRepository seriesRepo,
        ISubscriptionRepository subscriptionRepo,
        IPollingConfigRepository pollingConfigRepo)
    {
        _userManager = userManager;
        _seriesRepo = seriesRepo;
        _subscriptionRepo = subscriptionRepo;
        _pollingConfigRepo = pollingConfigRepo;
    }

    // ── Users ──

    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        List<ApplicationUser> users = _userManager.Users.ToList();
        List<UserDto> dtos = new List<UserDto>();

        foreach (ApplicationUser user in users)
        {
            IList<string> roles = await _userManager.GetRolesAsync(user);
            List<Subscription> subs = await _subscriptionRepo.GetByUserIdAsync(user.Id);
            dtos.Add(new UserDto
            {
                Id = user.Id,
                // ReSharper disable once NullableWarningSuppressionIsUsed
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? Roles.User,
                SubscribedSeries = subs.Select(s => s.Series.Name).ToList()
            });
        }

        return Ok(dtos);
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> SetRole(string userId, [FromBody] string role)
    {
        if (role != Roles.Admin && role != Roles.User)
            return BadRequest("Invalid role.");

        ApplicationUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        IList<string> currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        return NoContent();
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
            CreatedAt = s.CreatedAt,
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
    public async Task<IActionResult> UpdateSeries(int id, [FromBody] UpdateSeriesRequest request)
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
    public async Task<IActionResult> DeleteSeries(int id)
    {
        if (await _seriesRepo.GetByIdAsync(id) is null) return NotFound();
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
    public async Task<IActionResult> UpdatePollingConfig(int seriesId, [FromBody] UpdatePollingConfigRequest request)
    {
        PollingConfig? config = await _pollingConfigRepo.GetBySeriesIdAsync(seriesId);
        if (config is null) return NotFound();

        config.IntervalHours = request.IntervalHours;
        config.Enabled = request.Enabled;
        await _pollingConfigRepo.UpdateAsync(config);
        return NoContent();
    }
}