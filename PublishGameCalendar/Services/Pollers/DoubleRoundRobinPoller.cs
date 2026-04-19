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
        foreach (IElement el in document.QuerySelectorAll("div.match-fixture, div.match-result"))
        {
            Event? ev = ParseMatch(el, sourceUrl);
            if (ev is not null)
                events.Add(ev);
        }

        return events;
    }

    private Event? ParseMatch(IElement el, string sourceUrl)
    {
        string? eventHref = el.QuerySelector(".match-date a")?.GetAttribute("href");
        string? uid = ExtractEventId(eventHref);
        if (uid is null)
        {
            _logger.LogWarning("Skipping match element: could not extract event ID from '{Href}'", eventHref);
            return null;
        }

        string? homeHref = el.QuerySelector(".team-home a")?.GetAttribute("href");
        string? homeName = el.QuerySelector(".team-home a")?.TextContent.Trim();
        string? awayName = el.QuerySelector(".team-away a")?.TextContent.Trim();
        string? opponent = IsOurTeam(homeHref, sourceUrl) ? awayName : homeName;
        if (string.IsNullOrWhiteSpace(opponent))
        {
            _logger.LogWarning("Skipping match {Uid}: could not determine opponent", uid);
            return null;
        }

        string? dateText = el.QuerySelector(".match-date a")?.TextContent.Trim();
        if (!DateTime.TryParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime date))
        {
            _logger.LogWarning("Skipping match {Uid}: could not parse date '{Date}'", uid, dateText);
            return null;
        }

        string? timeText = el.QuerySelector(".match-time a")?.TextContent.Trim();
        DateTime start = BuildStartUtc(date, timeText);

        string? location = el.QuerySelector(".match-venue")?.TextContent.Trim();

        return new Event
        {
            Uid = $"wpmatch-{uid}",
            Title = opponent,
            Start = start,
            End = start.AddHours(2),
            Location = string.IsNullOrWhiteSpace(location) ? null : location
        };
    }

    private static string? ExtractEventId(string? href)
    {
        // href = "https://wpmatch.ch/event/263129/"
        if (href is null) return null;
        string[] parts = href.TrimEnd('/').Split('/');
        string id = parts[^1];
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static bool IsOurTeam(string? teamHref, string sourceUrl)
    {
        return string.Equals(teamHref?.TrimEnd('/'), sourceUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime BuildStartUtc(DateTime date, string? timeText)
    {
        // timeText format: "20h30"
        if (!string.IsNullOrWhiteSpace(timeText) && timeText.Contains('h'))
        {
            string[] parts = timeText.Split('h');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int hours) &&
                int.TryParse(parts[1], out int minutes))
                return DateTime.SpecifyKind(date.AddHours(hours).AddMinutes(minutes), DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }
}