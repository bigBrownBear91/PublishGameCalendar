using Amazon.DynamoDBv2.DataModel;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class DynamoDbSeriesRepository : ISeriesRepository
{
    private readonly IDynamoDbContext _db;
    private readonly IPollingConfigRepository _pollingConfigRepo;

    public DynamoDbSeriesRepository(IDynamoDbContext db, IPollingConfigRepository pollingConfigRepo)
    {
        _db = db;
        _pollingConfigRepo = pollingConfigRepo;
    }

    public async Task<List<Series>> GetAllAsync()
    {
        List<Series> series = await _db.ScanAsync<Series>(Array.Empty<ScanCondition>());
        foreach (Series s in series)
            s.PollingConfig = await _pollingConfigRepo.GetBySeriesIdAsync(s.Id);
        return series;
    }

    public async Task<Series?> GetByIdAsync(string id)
    {
        Series? series = await _db.LoadAsync<Series>(id);
        if (series is not null)
            series.PollingConfig = await _pollingConfigRepo.GetBySeriesIdAsync(id);
        return series;
    }

    public async Task<Series> CreateAsync(Series series)
    {
        await _db.SaveAsync(series);
        return series;
    }

    public Task UpdateAsync(Series series) => _db.SaveAsync(series);

    public Task DeleteAsync(string id) => _db.DeleteAsync<Series>(id);
}
