using PublishGameCalendar.Domain;
using PublishGameCalendar.Services.Enrichment;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class EventEnricherTests
{
    private static readonly DateTime Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc);

    private readonly EventEnricher _sut = new EventEnricher();

    [Fact]
    public void Merge_WhenNoEnrichments_ReturnsPolledEventsUnchanged()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End }];

        // Act
        List<Event> result = _sut.Merge(polled, []);

        // Assert
        Assert.Single(result);
        Assert.Equal("Match A", result[0].Title);
    }

    [Fact]
    public void Merge_WhenEnrichmentExistsAndPolledFieldIsEmpty_UsesEnrichmentValue()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End, Description = null }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Description = "Playoff game" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Playoff game", result[0].Description);
    }

    [Fact]
    public void Merge_WhenPolledFieldIsNonEmpty_PolledValueWins()
    {
        // Arrange
        List<Event> polled =
            [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End, Description = "Official info" }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Description = "Admin note" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Official info", result[0].Description);
    }

    [Fact]
    public void Merge_WhenPolledTitleIsEmpty_UsesEnrichmentTitle()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "", Start = Start, End = End }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Title = "Friendly Match" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Friendly Match", result[0].Title);
    }

    [Fact]
    public void Merge_WhenPolledTitleIsNonEmpty_PolledTitleWins()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Away Game", Start = Start, End = End }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Title = "Admin Title" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Away Game", result[0].Title);
    }

    [Fact]
    public void Merge_WhenPolledLocationIsEmpty_UsesEnrichmentLocation()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End, Location = null }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Location = "Home Arena" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Home Arena", result[0].Location);
    }

    [Fact]
    public void Merge_StartAndEndAreNeverOverriddenByEnrichment()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End }];
        List<EventEnrichment> enrichments = [new EventEnrichment { SeriesId = "s1", EventUid = "e1" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal(Start, result[0].Start);
        Assert.Equal(End, result[0].End);
    }

    [Fact]
    public void Merge_EventWithNoMatchingEnrichment_IsReturnedUnchanged()
    {
        // Arrange
        List<Event> polled = [new Event { Uid = "e1", Title = "Match A", Start = Start, End = End }];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e-other", Description = "Unrelated" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Null(result[0].Description);
    }

    [Fact]
    public void Merge_MultipleEvents_OnlyEnrichesMatchingUids()
    {
        // Arrange
        List<Event> polled =
        [
            new Event { Uid = "e1", Title = "Match A", Start = Start, End = End },
            new Event { Uid = "e2", Title = "Match B", Start = Start, End = End }
        ];
        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Description = "Final" }];

        // Act
        List<Event> result = _sut.Merge(polled, enrichments);

        // Assert
        Assert.Equal("Final", result[0].Description);
        Assert.Null(result[1].Description);
    }
}
