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
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IPollingConfigRepository pollingConfigRepo =
            scope.ServiceProvider.GetRequiredService<IPollingConfigRepository>();
        ISubscriptionRepository subscriptionRepo = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        IIcsService icsService = scope.ServiceProvider.GetRequiredService<IIcsService>();
        IPollerFactory pollerFactory = scope.ServiceProvider.GetRequiredService<IPollerFactory>();
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
        IPollerFactory pollerFactory,
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
            _logger.LogInformation("Fetched {Count} events for series '{Name}'", freshEvents.Count, series.Name);

            if (freshEvents.Count == 0)
            {
                _logger.LogError(
                    "Poller returned 0 events for series '{Name}' (id={Id}) — skipping update to prevent data loss. " +
                    "This may indicate a website restructuring.", series.Name, series.Id);
                config.LastPolledAt = now;
                config.LastPollFailed = true;
                await pollingConfigRepo.UpdateAsync(config);
                return;
            }

            EventDiff diff = await icsService.DiffAsync(series.Id, freshEvents);

            config.LastPolledAt = now;
            config.LastPollFailed = false;

            if (diff.HasChanges)
            {
                await icsService.WriteAsync(series.Id, freshEvents);
                config.LastChangeAt = now;

                List<Subscription> subscribers = await subscriptionRepo.GetBySeriesIdAsync(series.Id);
                // ReSharper disable once NullableWarningSuppressionIsUsed
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
            config.LastPolledAt = now;
            config.LastPollFailed = true;
            try { await pollingConfigRepo.UpdateAsync(config); }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to persist poll failure for series '{Name}' (id={Id})", series.Name, series.Id);
            }
        }
    }
}