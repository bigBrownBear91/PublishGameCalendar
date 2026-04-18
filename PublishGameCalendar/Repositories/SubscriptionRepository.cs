using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _db;

    public SubscriptionRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<Subscription>> GetByUserIdAsync(string userId)
    {
        return _db.Subscriptions.Include(s => s.Series).Where(s => s.UserId == userId).ToListAsync();
    }

    public Task<List<Subscription>> GetBySeriesIdAsync(int seriesId)
    {
        return _db.Subscriptions.Include(s => s.User).Where(s => s.SeriesId == seriesId).ToListAsync();
    }

    public Task<bool> ExistsAsync(string userId, int seriesId)
    {
        return _db.Subscriptions.AnyAsync(s => s.UserId == userId && s.SeriesId == seriesId);
    }

    public async Task CreateAsync(Subscription subscription)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string userId, int seriesId)
    {
        Subscription? sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SeriesId == seriesId);
        if (sub is not null)
        {
            _db.Subscriptions.Remove(sub);
            await _db.SaveChangesAsync();
        }
    }
}