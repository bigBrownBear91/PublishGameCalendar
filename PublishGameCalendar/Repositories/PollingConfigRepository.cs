using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class PollingConfigRepository : IPollingConfigRepository
{
    private readonly ApplicationDbContext _db;

    public PollingConfigRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<PollingConfig>> GetAllAsync()
    {
        return _db.PollingConfigs.Include(p => p.Series).ToListAsync();
    }

    public Task<List<PollingConfig>> GetAllEnabledAsync()
    {
        return _db.PollingConfigs.Include(p => p.Series)
            .Where(p => p.Enabled && p.Series.Enabled)
            .ToListAsync();
    }

    public Task<PollingConfig?> GetBySeriesIdAsync(int seriesId)
    {
        return _db.PollingConfigs.FirstOrDefaultAsync(p => p.SeriesId == seriesId);
    }

    public async Task CreateAsync(PollingConfig config)
    {
        _db.PollingConfigs.Add(config);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PollingConfig config)
    {
        _db.PollingConfigs.Update(config);
        await _db.SaveChangesAsync();
    }
}