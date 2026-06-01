using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Repositories;

public interface IEnrichmentRepository
{
    Task<List<EventEnrichment>> GetBySeriesIdAsync(string seriesId);
    Task UpsertAsync(EventEnrichment enrichment);
    Task DeleteAsync(string seriesId, string eventUid);
    Task DeleteAllBySeriesIdAsync(string seriesId);
}
