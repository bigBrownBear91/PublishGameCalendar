using Moq;
using PublishGameCalendar.Data.DynamoDb;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class SeriesRepositoryTests
{
    private static (Mock<IDynamoDbContext> db, Mock<IPollingConfigRepository> pollingRepo, DynamoDbSeriesRepository repo) Build()
    {
        Mock<IDynamoDbContext> db = new();
        Mock<IPollingConfigRepository> pollingRepo = new();
        pollingRepo.Setup(r => r.GetBySeriesIdAsync(It.IsAny<string>())).ReturnsAsync((PollingConfig?)null);
        DynamoDbSeriesRepository repo = new(db.Object, pollingRepo.Object);
        return (db, pollingRepo, repo);
    }

    [Fact]
    public async Task CreateAsync_SavesSeriesAndReturnsIt()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, _, DynamoDbSeriesRepository repo) = Build();
        Series series = new() { Id = "s1", Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller" };

        // Act
        Series result = await repo.CreateAsync(series);

        // Assert
        db.Verify(d => d.SaveAsync(series, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("s1", result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsSeries()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, _, DynamoDbSeriesRepository repo) = Build();
        Series stored = new() { Id = "s1", Name = "PL" };
        db.Setup(d => d.LoadAsync<Series>("s1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // Act
        Series? result = await repo.GetByIdAsync("s1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PL", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, _, DynamoDbSeriesRepository repo) = Build();
        db.Setup(d => d.LoadAsync<Series>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Series?)null);

        // Act
        Series? result = await repo.GetByIdAsync("missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_CallsDeleteOnContext()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, _, DynamoDbSeriesRepository repo) = Build();

        // Act
        await repo.DeleteAsync("s1");

        // Assert
        db.Verify(d => d.DeleteAsync<Series>("s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CallsSaveOnContext()
    {
        // Arrange
        (Mock<IDynamoDbContext> db, _, DynamoDbSeriesRepository repo) = Build();
        Series series = new() { Id = "s1", Name = "Updated" };

        // Act
        await repo.UpdateAsync(series);

        // Assert
        db.Verify(d => d.SaveAsync(series, It.IsAny<CancellationToken>()), Times.Once);
    }
}
