using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Identity;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;
using PublishGameCalendar.Services.Orchestrator;
using PublishGameCalendar.Services.Pollers;
using PublishGameCalendar.Services.Queue;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class OrchestratorServiceTests
{
    private static readonly List<Event> SomeEvents =
    [
        new Event { Uid = "e1", Title = "Opponent A", Start = DateTime.UtcNow, End = DateTime.UtcNow.AddHours(2) }
    ];

    private static IServiceProvider BuildProvider(
        IPollingConfigRepository pollingConfigRepo,
        ISubscriptionRepository subscriptionRepo,
        IIcsService icsService,
        IQueueAdapter queueAdapter,
        IPollerFactory pollerFactory)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(pollingConfigRepo);
        services.AddSingleton(subscriptionRepo);
        services.AddSingleton(icsService);
        services.AddSingleton(pollerFactory);
        services.AddSingleton(queueAdapter);
        return services.BuildServiceProvider();
    }

    private static Mock<IPollerFactory> PollerReturning(List<Event> events)
    {
        Mock<IWebsitePoller> poller = new Mock<IWebsitePoller>();
        poller.Setup(p => p.FetchEventsAsync(It.IsAny<Series>())).ReturnsAsync(events);
        Mock<IPollerFactory> factory = new Mock<IPollerFactory>();
        factory.Setup(f => f.Create(It.IsAny<string>())).Returns(poller.Object);
        return factory;
    }

    private static async Task RunOneTick(OrchestratorService sut, TaskCompletionSource tickComplete)
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sut.StartAsync(cts.Token);
        await tickComplete.Task.WaitAsync(cts.Token);
        cts.Cancel();
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenSeriesIsDue_CallsDiffOnIcsService()
    {
        // Arrange
        Series series = new Series { Id = 1, Name = "PL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 1, Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        Mock<ISubscriptionRepository> subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetBySeriesIdAsync(1)).ReturnsAsync([]);

        TaskCompletionSource tickComplete = new TaskCompletionSource();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffAsync(1, It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .ReturnsAsync(new EventDiff());

        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, subscriptionRepo.Object,
                    icsService.Object, new Mock<IQueueAdapter>().Object, PollerReturning(SomeEvents).Object)
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        icsService.Verify(s => s.DiffAsync(1, It.IsAny<List<Event>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenChangesDetected_PublishesNotification()
    {
        // Arrange
        Series series = new Series { Id = 2, Name = "CL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 2, Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        ApplicationUser fan = new ApplicationUser { Id = "u1", Email = "fan@test.com" };
        Mock<ISubscriptionRepository> subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetBySeriesIdAsync(2))
            .ReturnsAsync([new Subscription { UserId = "u1", SeriesId = 2, User = fan }]);

        EventDiff diff = new EventDiff { Added = [new Event { Uid = "e1", Title = "Final" }] };
        TaskCompletionSource tickComplete = new TaskCompletionSource();
        Mock<IQueueAdapter> queueAdapter = new Mock<IQueueAdapter>();
        queueAdapter.Setup(q => q.PublishAsync(It.IsAny<NotificationMessage>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffAsync(2, It.IsAny<List<Event>>())).ReturnsAsync(diff);

        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, subscriptionRepo.Object,
                    icsService.Object, queueAdapter.Object, PollerReturning(SomeEvents).Object)
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        queueAdapter.Verify(q => q.PublishAsync(It.Is<NotificationMessage>(m =>
            m.SeriesName == "CL" && m.RecipientEmails.Contains("fan@test.com"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenSeriesNotYetDue_SkipsPoll()
    {
        // Arrange — last polled 10 minutes ago, interval 1 hour
        Series series = new Series { Id = 3, Name = "EL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 3, Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = DateTime.UtcNow.AddMinutes(-10)
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        Mock<IIcsService> icsService = new Mock<IIcsService>();

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, new Mock<ISubscriptionRepository>().Object,
                    icsService.Object, new Mock<IQueueAdapter>().Object, PollerReturning(SomeEvents).Object)
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(300);

        // Assert
        icsService.Verify(s => s.DiffAsync(It.IsAny<int>(), It.IsAny<List<Event>>()), Times.Never);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenPollerReturnsZeroEvents_SkipsDiffAndWrite()
    {
        // Arrange
        Series series = new Series { Id = 4, Name = "BL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 4, Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        Mock<IIcsService> icsService = new Mock<IIcsService>();

        // Poller returns 0 events — simulates a website restructuring
        Mock<IPollerFactory> zeroEventsFactory = PollerReturning([]);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, new Mock<ISubscriptionRepository>().Object,
                    icsService.Object, new Mock<IQueueAdapter>().Object, zeroEventsFactory.Object)
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(300);

        // Assert — neither diff nor write was called, protecting existing data
        icsService.Verify(s => s.DiffAsync(It.IsAny<int>(), It.IsAny<List<Event>>()), Times.Never);
        icsService.Verify(s => s.WriteAsync(It.IsAny<int>(), It.IsAny<List<Event>>()), Times.Never);
    }
}
