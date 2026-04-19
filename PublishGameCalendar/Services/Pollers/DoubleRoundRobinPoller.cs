using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using PublishGameCalendar.Domain;

namespace PublishGameCalendar.Services.Pollers;

public class DoubleRoundRobinPoller : IWebsitePoller
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DoubleRoundRobinPoller> _logger;

    public DoubleRoundRobinPoller(HttpClient httpClient, ILogger<DoubleRoundRobinPoller> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<Event>> FetchEventsAsync(Series series)
    {
        string html = await _httpClient.GetStringAsync(series.SourceUrl);
        return await ParseAsync(html, series.SourceUrl);
    }

    internal async Task<List<Event>> ParseAsync(string html, string sourceUrl)
    {
        using IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        IDocument document = await context.OpenAsync(req => req.Content(html));

        List<Event> events = new();
        foreach (IElement row in document.QuerySelectorAll("tr.sp-row.sp-post"))
        {
            Event? ev = ParseRow(row, sourceUrl);
            if (ev is not null)
                events.Add(ev);
        }

        return events;
    }

    private Event? ParseRow(IElement row, string sourceUrl)
    {
        string? eventHref = row.QuerySelector("time.sp-event-date a")?.GetAttribute("href");
        string? uid = ExtractEventId(eventHref);
        if (uid is null)
        {
            _logger.LogWarning("Skipping row: could not extract event ID from '{Href}'", eventHref);
            return null;
        }

        string? opponent = null;
        foreach (IElement span in row.QuerySelectorAll("span.team-logo"))
        {
            if (!IsOurTeam(span.QuerySelector("a")?.GetAttribute("href"), sourceUrl))
            {
                opponent = span.QuerySelector("div.team-name")?.TextContent.Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(opponent))
        {
            _logger.LogWarning("Skipping event {Uid}: could not determine opponent", uid);
            return null;
        }

        IElement? timeEl = row.QuerySelector("time.sp-event-date");
        DateTime start = ParseStartUtc(timeEl?.GetAttribute("content"), timeEl?.GetAttribute("datetime"), uid);

        string? venue = row.QuerySelector("div.sp-event-venue")?.TextContent.Trim();

        return new Event
        {
            Uid = $"wpmatch-{uid}",
            Title = opponent,
            Start = start,
            End = start.AddHours(2),
            Location = string.IsNullOrWhiteSpace(venue) || venue == "N/A" ? null : venue
        };
    }

    private static string? ExtractEventId(string? href)
    {
        if (href is null) return null;
        string[] parts = href.TrimEnd('/').Split('/');
        string id = parts[^1];
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static bool IsOurTeam(string? teamHref, string sourceUrl)
    {
        return string.Equals(teamHref?.TrimEnd('/'), sourceUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private DateTime ParseStartUtc(string? content, string? datetimeAttr, string uid)
    {
        if (!string.IsNullOrWhiteSpace(content) &&
            DateTimeOffset.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dto))
            return dto.UtcDateTime;

        if (!string.IsNullOrWhiteSpace(datetimeAttr) &&
            DateTime.TryParse(datetimeAttr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
        {
            _logger.LogWarning("Event {Uid}: using datetime attribute without timezone offset", uid);
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        _logger.LogWarning("Event {Uid}: could not parse datetime", uid);
        return DateTime.MinValue;
    }
}
