using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public class DynamoDbEnrichmentRepository : IEnrichmentRepository
{
    private readonly IDynamoDbContext _db;

    public DynamoDbEnrichmentRepository(IDynamoDbContext db)
    {
        _db = db;
    }

    public Task<List<EventEnrichment>> GetBySeriesIdAsync(string seriesId) =>
        _db.QueryAsync<EventEnrichment>(seriesId);

    public Task UpsertAsync(EventEnrichment enrichment) =>
        _db.SaveAsync(enrichment);

    public Task DeleteAsync(string seriesId, string eventUid) =>
        _db.DeleteAsync<EventEnrichment>(seriesId, eventUid);

    public async Task DeleteAllBySeriesIdAsync(string seriesId)
    {
        List<EventEnrichment> enrichments = await GetBySeriesIdAsync(seriesId);
        await Task.WhenAll(enrichments.Select(e => _db.DeleteAsync<EventEnrichment>(e.SeriesId, e.EventUid)));
    }
}
