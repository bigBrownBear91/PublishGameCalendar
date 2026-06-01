using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Enrichment;

public class EventEnricher : IEventEnricher
{
    public List<Event> Merge(List<Event> polledEvents, IEnumerable<EventEnrichment> enrichments)
    {
        Dictionary<string, EventEnrichment> byUid = enrichments.ToDictionary(e => e.EventUid);
        return polledEvents.Select(e => MergeOne(e, byUid.GetValueOrDefault(e.Uid))).ToList();
    }

    private static Event MergeOne(Event polled, EventEnrichment? enrichment)
    {
        if (enrichment is null)
            return polled;

        return new Event
        {
            Uid = polled.Uid,
            Start = polled.Start,
            End = polled.End,
            Title = string.IsNullOrEmpty(polled.Title) ? (enrichment.Title ?? polled.Title) : polled.Title,
            Location = string.IsNullOrEmpty(polled.Location) ? (enrichment.Location ?? polled.Location) : polled.Location,
            Description = string.IsNullOrEmpty(polled.Description) ? (enrichment.Description ?? polled.Description) : polled.Description
        };
    }
}
