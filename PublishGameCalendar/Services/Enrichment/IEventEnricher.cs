using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Enrichment;

public interface IEventEnricher
{
    List<Event> Merge(List<Event> polledEvents, IEnumerable<EventEnrichment> enrichments);
}
