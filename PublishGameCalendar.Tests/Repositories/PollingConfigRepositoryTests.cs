using Amazon.DynamoDBv2.DataModel;
using Moq;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class PollingConfigRepositoryTests
{
    private static (Mock<IDynamoDbContext> db, DynamoDbPollingConfigRepository repo) Build()
    {
        Mock<IDynamoDbContext> db = new();
        return (db, new DynamoDbPollingConfigRepository(db.Object));
    }

    [Fact]
    public async Task GetAllEnabledAsync_ReturnsOnlyConfigsWhereSeriesIsAlsoEnabled()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbPollingConfigRepository repo) = Build();

        Series enabledSeries = new() { Id = "s1", Enabled = true };
        Series disabledSeries = new() { Id = "s2", Enabled = false };

        List<PollingConfig> scannedConfigs = new()
        {
            new PollingConfig { SeriesId = "s1", Enabled = true },
            new PollingConfig { SeriesId = "s2", Enabled = true }
        };

        db.Setup(d => d.ScanAsync<PollingConfig>(It.IsAny<IEnumerable<ScanCondition>>()))
            .ReturnsAsync(scannedConfigs);
        db.Setup(d => d.LoadAsync<Series>("s1", It.IsAny<CancellationToken>())).ReturnsAsync(enabledSeries);
        db.Setup(d => d.LoadAsync<Series>("s2", It.IsAny<CancellationToken>())).ReturnsAsync(disabledSeries);

        // Act
        List<PollingConfig> result = await repo.GetAllEnabledAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("s1", result[0].SeriesId);
    }

    [Fact]
    public async Task UpdateAsync_CallsSaveOnContext()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbPollingConfigRepository repo) = Build();
        PollingConfig config = new() { SeriesId = "s1", IntervalHours = 6 };

        // Act
        await repo.UpdateAsync(config);

        // Assert
        db.Verify(d => d.SaveAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySeriesIdAsync_DelegatesToLoadAsync()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, DynamoDbPollingConfigRepository repo) = Build();
        PollingConfig stored = new() { SeriesId = "s1", IntervalHours = 2 };
        db.Setup(d => d.LoadAsync<PollingConfig>("s1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // Act
        PollingConfig? result = await repo.GetBySeriesIdAsync("s1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.IntervalHours);
    }
}
