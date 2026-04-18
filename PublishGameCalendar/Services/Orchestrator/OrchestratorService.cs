using PublishGameCalendar.Domain;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Ics;
using PublishGameCalendar.Services.Pollers;
using PublishGameCalendar.Services.Queue;

namespace PublishGameCalendar.Services.Orchestrator;

public class OrchestratorService : BackgroundService
{
    private readonly ILogger<OrchestratorService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public OrchestratorService(IServiceScopeFactory scopeFactory, ILogger<OrchestratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollDueSeriesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task PollDueSeriesAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IPollingConfigRepository pollingConfigRepo =
            scope.ServiceProvider.GetRequiredService<IPollingConfigRepository>();
        ISubscriptionRepository subscriptionRepo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        IIcsService icsService = scope.ServiceProvider.GetRequiredService<IIcsService>();
        PollerFactory pollerFactory = scope.ServiceProvider.GetRequiredService<PollerFactory>();
        IQueueAdapter queueAdapter = scope.ServiceProvider.GetRequiredService<IQueueAdapter>();

        List<PollingConfig> configs = await pollingConfigRepo.GetAllEnabledAsync();
        DateTime now = DateTime.UtcNow;

        foreach (PollingConfig config in configs)
        {
            if (!IsDue(config, now)) continue;

            await PollSeriesAsync(config, pollingConfigRepo, subscriptionRepo,
                icsService, pollerFactory, queueAdapter, now, ct);
        }
    }

    private static bool IsDue(PollingConfig config, DateTime now)
    {
        return !config.LastPolledAt.HasValue ||
               config.LastPolledAt.Value.AddHours(config.IntervalHours) <= now;
    }

    private async Task PollSeriesAsync(
        PollingConfig config,
        IPollingConfigRepository pollingConfigRepo,
        ISubscriptionRepository subscriptionRepo,
        IIcsService icsService,
        PollerFactory pollerFactory,
        IQueueAdapter queueAdapter,
        DateTime now,
        CancellationToken ct)
    {
        Series series = config.Series;
        _logger.LogInformation("Polling series '{Name}' (id={Id})", series.Name, series.Id);

        try
        {
            IWebsitePoller poller = pollerFactory.Create(series.PollerType);
            List<Event> freshEvents = await poller.FetchEventsAsync(series);

            EventDiff diff = await icsService.DiffAsync(series.Id, freshEvents);

            config.LastPolledAt = now;

            if (diff.HasChanges)
            {
                await icsService.WriteAsync(series.Id, freshEvents);
                config.LastChangeAt = now;

                List<Subscription> subscribers = await subscriptionRepo.GetBySeriesIdAsync(series.Id);
                List<string> emails = subscribers.Select(s => s.User.Email!).Where(e => !string.IsNullOrEmpty(e))
                    .ToList();

                if (emails.Count > 0)
                    await queueAdapter.PublishAsync(new NotificationMessage
                    {
                        SeriesName = series.Name,
                        ChangeSummary = diff.BuildSummary(),
                        RecipientEmails = emails
                    });
            }

            await pollingConfigRepo.UpdateAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling series '{Name}' (id={Id})", series.Name, series.Id);
        }
    }
}