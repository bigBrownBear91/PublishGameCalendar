using Microsoft.EntityFrameworkCore;
using PublishGameCalendar.Data;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Identity;
using PublishGameCalendar.Repositories;
using Xunit;

namespace PublishGameCalendar.Tests.Repositories;

public class SubscriptionRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationUser user, Series series)> SeedAsync(ApplicationDbContext db)
    {
        ApplicationUser user = new ApplicationUser
            { Id = Guid.NewGuid().ToString(), UserName = "u@test.com", Email = "u@test.com" };
        Series series = new Series { Name = "PL", SourceUrl = "http://x.com", PollerType = "StubPoller" };
        db.Users.Add(user);
        db.Series.Add(series);
        await db.SaveChangesAsync();
        return (user, series);
    }

    [Fact]
    public async Task CreateAsync_ThenExistsAsync_ReturnsTrue()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(CreateAsync_ThenExistsAsync_ReturnsTrue));
        SubscriptionRepository repo = new SubscriptionRepository(db);
        (ApplicationUser user, Series series) = await SeedAsync(db);

        // Act
        await repo.CreateAsync(new Subscription { UserId = user.Id, SeriesId = series.Id });

        // Assert
        Assert.True(await repo.ExistsAsync(user.Id, series.Id));
    }

    [Fact]
    public async Task DeleteAsync_ThenExistsAsync_ReturnsFalse()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(DeleteAsync_ThenExistsAsync_ReturnsFalse));
        SubscriptionRepository repo = new SubscriptionRepository(db);
        (ApplicationUser user, Series series) = await SeedAsync(db);
        await repo.CreateAsync(new Subscription { UserId = user.Id, SeriesId = series.Id });

        // Act
        await repo.DeleteAsync(user.Id, series.Id);

        // Assert
        Assert.False(await repo.ExistsAsync(user.Id, series.Id));
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyUserSubscriptions()
    {
        // Arrange
        using ApplicationDbContext db = CreateContext(nameof(GetByUserIdAsync_ReturnsOnlyUserSubscriptions));
        SubscriptionRepository repo = new SubscriptionRepository(db);
        (ApplicationUser user, Series series) = await SeedAsync(db);

        ApplicationUser otherUser = new ApplicationUser
            { Id = Guid.NewGuid().ToString(), UserName = "other@test.com", Email = "other@test.com" };
        Series series2 = new Series { Name = "CL", SourceUrl = "http://y.com", PollerType = "StubPoller" };
        db.Users.Add(otherUser);
        db.Series.Add(series2);
        await db.SaveChangesAsync();

        await repo.CreateAsync(new Subscription { UserId = user.Id, SeriesId = series.Id });
        await repo.CreateAsync(new Subscription { UserId = otherUser.Id, SeriesId = series2.Id });

        // Act
        List<Subscription> subs = await repo.GetByUserIdAsync(user.Id);

        // Assert
        Assert.Single(subs);
        Assert.Equal(series.Id, subs[0].SeriesId);
    }
}