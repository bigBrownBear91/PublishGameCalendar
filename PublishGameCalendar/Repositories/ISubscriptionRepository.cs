using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public interface ISubscriptionRepository
{
    Task<List<Subscription>> GetByUserIdAsync(string userId);
    Task<List<Subscription>> GetBySeriesIdAsync(int seriesId);
    Task<bool> ExistsAsync(string userId, int seriesId);
    Task CreateAsync(Subscription subscription);
    Task DeleteAsync(string userId, int seriesId);
}