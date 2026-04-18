using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public interface ISeriesRepository
{
    Task<List<Series>> GetAllAsync();
    Task<Series?> GetByIdAsync(int id);
    Task<Series> CreateAsync(Series series);
    Task UpdateAsync(Series series);
    Task DeleteAsync(int id);
}