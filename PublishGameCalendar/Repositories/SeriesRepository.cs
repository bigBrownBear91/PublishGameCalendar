using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class SeriesRepository : ISeriesRepository
{
    private readonly ApplicationDbContext _db;

    public SeriesRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<Series>> GetAllAsync()
    {
        return _db.Series.Include(s => s.PollingConfig).ToListAsync();
    }

    public Task<Series?> GetByIdAsync(int id)
    {
        return _db.Series.Include(s => s.PollingConfig).FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Series> CreateAsync(Series series)
    {
        _db.Series.Add(series);
        await _db.SaveChangesAsync();
        return series;
    }

    public async Task UpdateAsync(Series series)
    {
        _db.Series.Update(series);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        Series? series = await _db.Series.FindAsync(id);
        if (series is not null)
        {
            _db.Series.Remove(series);
            await _db.SaveChangesAsync();
        }
    }
}