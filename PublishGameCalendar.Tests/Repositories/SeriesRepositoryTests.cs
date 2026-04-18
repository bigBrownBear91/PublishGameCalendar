using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class SeriesRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_PersistsSeries()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(CreateAsync_PersistsSeries));
        SeriesRepository repo = new SeriesRepository(db);
        Series series = new Series { Name = "Test", SourceUrl = "http://x.com", PollerType = "StubPoller" };

        // Act
        await repo.CreateAsync(series);

        // Assert
        Assert.Equal(1, await db.Series.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsSeries()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(GetByIdAsync_WhenExists_ReturnsSeries));
        SeriesRepository repo = new SeriesRepository(db);
        Series created = await repo.CreateAsync(new Series
            { Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller" });

        // Act
        Series? result = await repo.GetByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PL", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(GetByIdAsync_WhenNotExists_ReturnsNull));
        SeriesRepository repo = new SeriesRepository(db);

        // Act
        Series? result = await repo.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSeries()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(DeleteAsync_RemovesSeries));
        SeriesRepository repo = new SeriesRepository(db);
        Series created = await repo.CreateAsync(new Series
            { Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller" });

        // Act
        await repo.DeleteAsync(created.Id);

        // Assert
        Assert.Equal(0, await db.Series.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(UpdateAsync_PersistsChanges));
        SeriesRepository repo = new SeriesRepository(db);
        Series created = await repo.CreateAsync(new Series
            { Name = "Old", SourceUrl = "http://x.com", PollerType = "StubPoller" });

        // Act
        created.Name = "New";
        await repo.UpdateAsync(created);

        // Assert
        Series? updated = await repo.GetByIdAsync(created.Id);
        Assert.Equal("New", updated!.Name);
    }
}