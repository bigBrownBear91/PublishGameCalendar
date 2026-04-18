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
    private static IServiceProvider BuildProvider(
        IPollingConfigRepository pollingConfigRepo,
        ISubscriptionRepository subscriptionRepo,
        IIcsService icsService,
        IQueueAdapter queueAdapter)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(pollingConfigRepo);
        services.AddSingleton(subscriptionRepo);
        services.AddSingleton(icsService);
        services.AddTransient<StubPoller>();
        services.AddTransient<PollerFactory>();
        services.AddSingleton(queueAdapter);
        return services.BuildServiceProvider();
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
        Series series = new Series { Id = 1, Name = "PL", PollerType = nameof(StubPoller), Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 1, Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync(new List<PollingConfig> { config });

        Mock<ISubscriptionRepository> subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetBySeriesIdAsync(1)).ReturnsAsync(new List<Subscription>());

        TaskCompletionSource tickComplete = new TaskCompletionSource();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffAsync(1, It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .ReturnsAsync(new EventDiff());

        Mock<IQueueAdapter> queueAdapter = new Mock<IQueueAdapter>();

        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, subscriptionRepo.Object,
                    icsService.Object, queueAdapter.Object)
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
        Series series = new Series { Id = 2, Name = "CL", PollerType = nameof(StubPoller), Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 2, Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync(new List<PollingConfig> { config });

        ApplicationUser fan = new ApplicationUser { Id = "u1", Email = "fan@test.com" };
        Mock<ISubscriptionRepository> subscriptionRepo = new Mock<ISubscriptionRepository>();
        subscriptionRepo.Setup(r => r.GetBySeriesIdAsync(2))
            .ReturnsAsync(new List<Subscription> { new Subscription { UserId = "u1", SeriesId = 2, User = fan } });

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
                    icsService.Object, queueAdapter.Object)
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
        Series series = new Series { Id = 3, Name = "EL", PollerType = nameof(StubPoller), Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = 3, Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = DateTime.UtcNow.AddMinutes(-10)
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync(new List<PollingConfig> { config });

        Mock<ISubscriptionRepository> subscriptionRepo = new Mock<ISubscriptionRepository>();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        Mock<IQueueAdapter> queueAdapter = new Mock<IQueueAdapter>();

        // Use a short-lived cancellation so the test doesn't block indefinitely.
        // We deliberately expect NO calls, so we just let the orchestrator run one cycle.
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        OrchestratorService sut = new OrchestratorService(
            BuildProvider(pollingConfigRepo.Object, subscriptionRepo.Object,
                    icsService.Object, queueAdapter.Object)
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(300); // let orchestrator tick; no poll should happen

        // Assert
        icsService.Verify(s => s.DiffAsync(It.IsAny<int>(), It.IsAny<List<Event>>()), Times.Never);
    }
}