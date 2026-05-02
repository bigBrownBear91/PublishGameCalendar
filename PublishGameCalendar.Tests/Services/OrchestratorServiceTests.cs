using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;
using PublishGameCalendar.Services.Orchestrator;
using PublishGameCalendar.Services.Pollers;
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
        IIcsService icsService,
        IPollerFactory pollerFactory)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(pollingConfigRepo);
        services.AddSingleton(icsService);
        services.AddSingleton(pollerFactory);
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

    private static OrchestratorService BuildSut(IServiceProvider provider) =>
        new OrchestratorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrchestratorService>.Instance);

    [Fact]
    public async Task PollDueSeriesAsync_WhenSeriesIsDue_CallsDiffOnIcsService()
    {
        // Arrange
        Series series = new Series { Id = "s1", Name = "PL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s1", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        TaskCompletionSource tickComplete = new TaskCompletionSource();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffAsync("s1", It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .ReturnsAsync(new EventDiff());

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        icsService.Verify(s => s.DiffAsync("s1", It.IsAny<List<Event>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenSeriesNotYetDue_SkipsPoll()
    {
        // Arrange — last polled 10 minutes ago, interval 1 hour
        Series series = new Series { Id = "s2", Name = "EL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s2", Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = DateTime.UtcNow.AddMinutes(-10)
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        Mock<IIcsService> icsService = new Mock<IIcsService>();

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(300);

        // Assert
        icsService.Verify(s => s.DiffAsync(It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenPollerReturnsZeroEvents_SkipsDiffAndWrite()
    {
        // Arrange
        Series series = new Series { Id = "s3", Name = "BL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s3", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        Mock<IIcsService> icsService = new Mock<IIcsService>();

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning([]).Object));

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(300);

        // Assert — neither diff nor write was called, protecting existing data
        icsService.Verify(s => s.DiffAsync(It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
        icsService.Verify(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenPollerReturnsZeroEvents_SetsLastPollFailedAndUpdatesConfig()
    {
        // Arrange
        Series series = new Series { Id = "s4", Name = "RL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s4", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);
        TaskCompletionSource tickComplete = new TaskCompletionSource();
        pollingConfigRepo.Setup(r => r.UpdateAsync(It.IsAny<PollingConfig>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, new Mock<IIcsService>().Object, PollerReturning([]).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        pollingConfigRepo.Verify(r => r.UpdateAsync(It.Is<PollingConfig>(c =>
            c.LastPollFailed == true && c.LastPolledAt.HasValue)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenPollerSucceeds_ClearsLastPollFailed()
    {
        // Arrange — config previously marked as failed
        Series series = new Series { Id = "s5", Name = "SL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s5", Series = series, IntervalHours = 1, Enabled = true,
            LastPolledAt = null, LastPollFailed = true
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);
        TaskCompletionSource tickComplete = new TaskCompletionSource();
        pollingConfigRepo.Setup(r => r.UpdateAsync(It.IsAny<PollingConfig>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffAsync("s5", It.IsAny<List<Event>>())).ReturnsAsync(new EventDiff());

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert — LastPollFailed cleared on successful poll
        pollingConfigRepo.Verify(r => r.UpdateAsync(It.Is<PollingConfig>(c =>
            c.LastPollFailed == false)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenPollerThrows_SetsLastPollFailedAndSavesConfig()
    {
        // Arrange
        Series series = new Series { Id = "s6", Name = "CUP", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s6", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);
        TaskCompletionSource tickComplete = new TaskCompletionSource();
        pollingConfigRepo.Setup(r => r.UpdateAsync(It.IsAny<PollingConfig>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        Mock<IWebsitePoller> poller = new Mock<IWebsitePoller>();
        poller.Setup(p => p.FetchEventsAsync(It.IsAny<Series>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        Mock<IPollerFactory> factory = new Mock<IPollerFactory>();
        factory.Setup(f => f.Create(It.IsAny<string>())).Returns(poller.Object);

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, new Mock<IIcsService>().Object, factory.Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert — failure recorded even when poller throws
        pollingConfigRepo.Verify(r => r.UpdateAsync(It.Is<PollingConfig>(c =>
            c.LastPollFailed == true && c.LastPolledAt.HasValue)), Times.AtLeastOnce);
    }
}
