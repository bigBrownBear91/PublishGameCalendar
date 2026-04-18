using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class PollingConfigRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAllEnabledAsync_ReturnsOnlyEnabledConfigsWithEnabledSeries()
    {
        // Arrange
        using ApplicationDbContext db =
            CreateContext(nameof(GetAllEnabledAsync_ReturnsOnlyEnabledConfigsWithEnabledSeries));
        Series enabledSeries = new Series
            { Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller", Enabled = true };
        Series disabledSeries = new Series
            { Name = "CL", SourceUrl = "http://y.com", PollerType = "StubPoller", Enabled = false };
        db.Series.AddRange(enabledSeries, disabledSeries);
        await db.SaveChangesAsync();

        db.PollingConfigs.Add(new PollingConfig { SeriesId = enabledSeries.Id, Enabled = true });
        db.PollingConfigs.Add(new PollingConfig { SeriesId = disabledSeries.Id, Enabled = true });
        await db.SaveChangesAsync();

        PollingConfigRepository repo = new PollingConfigRepository(db);

        // Act
        List<PollingConfig> enabled = await repo.GetAllEnabledAsync();

        // Assert
        Assert.Single(enabled);
        Assert.Equal(enabledSeries.Id, enabled[0].SeriesId);
    }

    [Fact]
    public async Task UpdateAsync_PersistsIntervalChange()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(UpdateAsync_PersistsIntervalChange));
        Series series = new Series { Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller" };
        db.Series.Add(series);
        await db.SaveChangesAsync();

        PollingConfig config = new PollingConfig { SeriesId = series.Id, IntervalHours = 1 };
        db.PollingConfigs.Add(config);
        await db.SaveChangesAsync();

        PollingConfigRepository repo = new PollingConfigRepository(db);

        // Act
        config.IntervalHours = 6;
        await repo.UpdateAsync(config);

        // Assert
        PollingConfig? updated = await repo.GetBySeriesIdAsync(series.Id);
        Assert.Equal(6, updated!.IntervalHours);
    }
}