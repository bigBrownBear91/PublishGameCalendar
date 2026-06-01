using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Enrichment;
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
        IPollerFactory pollerFactory,
        IEnrichmentRepository? enrichmentRepo = null,
        IEventEnricher? eventEnricher = null)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(pollingConfigRepo);
        services.AddSingleton(icsService);
        services.AddSingleton(pollerFactory);
        services.AddSingleton(enrichmentRepo ?? DefaultEnrichmentRepo().Object);
        services.AddSingleton(eventEnricher ?? new EventEnricher());
        return services.BuildServiceProvider();
    }

    private static Mock<IEnrichmentRepository> DefaultEnrichmentRepo()
    {
        Mock<IEnrichmentRepository> repo = new Mock<IEnrichmentRepository>();
        repo.Setup(r => r.GetBySeriesIdAsync(It.IsAny<string>())).ReturnsAsync([]);
        return repo;
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
    public async Task PollDueSeriesAsync_WhenSeriesIsDue_CallsDiffRawOnIcsService()
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
        icsService.Setup(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .ReturnsAsync(new EventDiff());

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        icsService.Verify(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenSeriesIsDue_AlwaysWritesRawSnapshot()
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
        icsService.Setup(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>())).ReturnsAsync(new EventDiff());
        icsService.Setup(s => s.WriteRawSnapshotAsync("s1", It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert — raw snapshot always written after successful poll
        icsService.Verify(s => s.WriteRawSnapshotAsync("s1", SomeEvents), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenChangesDetected_WritesEnrichedIcs()
    {
        // Arrange
        Series series = new Series { Id = "s1", Name = "PL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s1", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        EventDiff diffWithChanges = new EventDiff();
        diffWithChanges.Added.Add(SomeEvents[0]);

        TaskCompletionSource tickComplete = new TaskCompletionSource();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>())).ReturnsAsync(diffWithChanges);
        icsService.Setup(s => s.WriteAsync("s1", "PL", It.IsAny<List<Event>>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert
        icsService.Verify(s => s.WriteAsync("s1", "PL", It.IsAny<List<Event>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_WhenChangesDetected_MergesEnrichmentsBeforeWrite()
    {
        // Arrange
        Series series = new Series { Id = "s1", Name = "PL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s1", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        EventDiff diffWithChanges = new EventDiff();
        diffWithChanges.Added.Add(SomeEvents[0]);

        List<EventEnrichment> enrichments =
            [new EventEnrichment { SeriesId = "s1", EventUid = "e1", Description = "Final" }];

        Mock<IEnrichmentRepository> enrichmentRepo = new Mock<IEnrichmentRepository>();
        // Only one query issued (reused for both orphan cleanup and merge)
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync(enrichments);

        Mock<IEventEnricher> eventEnricher = new Mock<IEventEnricher>();
        TaskCompletionSource tickComplete = new TaskCompletionSource();
        eventEnricher.Setup(e => e.Merge(It.IsAny<List<Event>>(), It.IsAny<IEnumerable<EventEnrichment>>()))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(SomeEvents);

        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>())).ReturnsAsync(diffWithChanges);

        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object,
            enrichmentRepo.Object, eventEnricher.Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert — enrichments fetched exactly once and passed to merger
        enrichmentRepo.Verify(r => r.GetBySeriesIdAsync("s1"), Times.Once);
        eventEnricher.Verify(e => e.Merge(It.IsAny<List<Event>>(), It.IsAny<IEnumerable<EventEnrichment>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollDueSeriesAsync_DeletesOrphanEnrichmentsAfterPoll()
    {
        // Arrange
        Series series = new Series { Id = "s1", Name = "PL", PollerType = "any", Enabled = true };
        PollingConfig config = new PollingConfig
        {
            SeriesId = "s1", Series = series, IntervalHours = 1, Enabled = true, LastPolledAt = null
        };

        Mock<IPollingConfigRepository> pollingConfigRepo = new Mock<IPollingConfigRepository>();
        pollingConfigRepo.Setup(r => r.GetAllEnabledAsync()).ReturnsAsync([config]);

        List<EventEnrichment> enrichments =
        [
            new EventEnrichment { SeriesId = "s1", EventUid = "e1" },   // still live
            new EventEnrichment { SeriesId = "s1", EventUid = "e-gone" } // orphan
        ];

        Mock<IEnrichmentRepository> enrichmentRepo = new Mock<IEnrichmentRepository>();
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync(enrichments);

        TaskCompletionSource tickComplete = new TaskCompletionSource();
        enrichmentRepo.Setup(r => r.DeleteAsync("s1", "e-gone"))
            .Callback(() => tickComplete.TrySetResult())
            .Returns(Task.CompletedTask);

        Mock<IIcsService> icsService = new Mock<IIcsService>();
        icsService.Setup(s => s.DiffRawAsync("s1", It.IsAny<List<Event>>())).ReturnsAsync(new EventDiff());

        // Poller returns only e1; e-gone is not in fresh events
        OrchestratorService sut = BuildSut(BuildProvider(
            pollingConfigRepo.Object, icsService.Object, PollerReturning(SomeEvents).Object,
            enrichmentRepo.Object));

        // Act
        await RunOneTick(sut, tickComplete);

        // Assert — orphan deleted, live enrichment kept
        enrichmentRepo.Verify(r => r.DeleteAsync("s1", "e-gone"), Times.AtLeastOnce);
        enrichmentRepo.Verify(r => r.DeleteAsync("s1", "e1"), Times.Never);
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
        icsService.Verify(s => s.DiffRawAsync(It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
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
        icsService.Verify(s => s.DiffRawAsync(It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
        icsService.Verify(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
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
        icsService.Setup(s => s.DiffRawAsync("s5", It.IsAny<List<Event>>())).ReturnsAsync(new EventDiff());

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
