using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
// ReSharper disable NullableWarningSuppressionIsUsed

namespace PublishGameCalendar.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISeriesRepository _seriesRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;

    public SubscriptionController(ISubscriptionRepository subscriptionRepo, ISeriesRepository seriesRepo)
    {
        _subscriptionRepo = subscriptionRepo;
        _seriesRepo = seriesRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetMySubscriptions()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        List<Subscription> subs = await _subscriptionRepo.GetByUserIdAsync(userId);
        List<int> ids = subs.Select(s => s.SeriesId).ToList();
        return Ok(ids);
    }

    [HttpPost("{seriesId}")]
    public async Task<IActionResult> Subscribe(int seriesId)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (await _seriesRepo.GetByIdAsync(seriesId) is null)
            return NotFound();

        if (await _subscriptionRepo.ExistsAsync(userId, seriesId))
            return Conflict("Already subscribed.");

        await _subscriptionRepo.CreateAsync(new Subscription { UserId = userId, SeriesId = seriesId });
        return NoContent();
    }

    [HttpDelete("{seriesId}")]
    public async Task<IActionResult> Unsubscribe(int seriesId)
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!await _subscriptionRepo.ExistsAsync(userId, seriesId))
            return NotFound();

        await _subscriptionRepo.DeleteAsync(userId, seriesId);
        return NoContent();
    }
}