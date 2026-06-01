using Moq;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class EnrichmentRepositoryTests
{
    private static (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) Build()
    {
        Mock<IDynamoDbContext> db = new();
        DynamoDbEnrichmentRepository repo = new(db.Object);
        return (db, repo);
    }

    [Fact]
    public async Task GetBySeriesIdAsync_QueriesContextWithSeriesId()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) = Build();
        List<EventEnrichment> stored = [new EventEnrichment { SeriesId = "s1", EventUid = "e1" }];
        db.Setup(d => d.QueryAsync<EventEnrichment>("s1")).ReturnsAsync(stored);

        // Act
        List<EventEnrichment> result = await repo.GetBySeriesIdAsync("s1");

        // Assert
        Assert.Single(result);
        Assert.Equal("e1", result[0].EventUid);
    }

    [Fact]
    public async Task UpsertAsync_SavesEnrichmentToContext()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) = Build();
        EventEnrichment enrichment = new() { SeriesId = "s1", EventUid = "e1", Description = "Final" };

        // Act
        await repo.UpsertAsync(enrichment);

        // Assert
        db.Verify(d => d.SaveAsync(enrichment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesByCompositeKey()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) = Build();

        // Act
        await repo.DeleteAsync("s1", "e1");

        // Assert
        db.Verify(d => d.DeleteAsync<EventEnrichment>("s1", "e1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAllBySeriesIdAsync_DeletesEachEnrichmentForSeries()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) = Build();
        List<EventEnrichment> stored =
        [
            new EventEnrichment { SeriesId = "s1", EventUid = "e1" },
            new EventEnrichment { SeriesId = "s1", EventUid = "e2" }
        ];
        db.Setup(d => d.QueryAsync<EventEnrichment>("s1")).ReturnsAsync(stored);

        // Act
        await repo.DeleteAllBySeriesIdAsync("s1");

        // Assert
        db.Verify(d => d.DeleteAsync<EventEnrichment>("s1", "e1", It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(d => d.DeleteAsync<EventEnrichment>("s1", "e2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAllBySeriesIdAsync_WhenNoEnrichments_DoesNotCallDelete()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbEnrichmentRepository repo) = Build();
        db.Setup(d => d.QueryAsync<EventEnrichment>("s1")).ReturnsAsync([]);

        // Act
        await repo.DeleteAllBySeriesIdAsync("s1");

        // Assert
        db.Verify(d => d.DeleteAsync<EventEnrichment>(It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
