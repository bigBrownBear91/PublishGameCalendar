using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class DynamoDbPollingConfigRepository : IPollingConfigRepository
{
    private readonly IDynamoDbContext _db;

    public DynamoDbPollingConfigRepository(IDynamoDbContext db)
    {
        _db = db;
    }

    public async Task<List<PollingConfig>> GetAllAsync()
    {
        List<PollingConfig> configs = await _db.ScanAsync<PollingConfig>(Array.Empty<ScanCondition>());
        await PopulateSeriesAsync(configs);
        return configs;
    }

    public async Task<List<PollingConfig>> GetAllEnabledAsync()
    {
        List<PollingConfig> configs = await _db.ScanAsync<PollingConfig>(
            new[] { new ScanCondition("Enabled", ScanOperator.Equal, true) });
        await PopulateSeriesAsync(configs);
        return configs.Where(c => c.Series.Enabled).ToList();
    }

    public Task<PollingConfig?> GetBySeriesIdAsync(string seriesId) =>
        _db.LoadAsync<PollingConfig>(seriesId);

    public Task CreateAsync(PollingConfig config) => _db.SaveAsync(config);

    public Task UpdateAsync(PollingConfig config) => _db.SaveAsync(config);

    private async Task PopulateSeriesAsync(List<PollingConfig> configs)
    {
        foreach (PollingConfig config in configs)
            config.Series = await _db.LoadAsync<Series>(config.SeriesId) ?? new Series();
    }
}
