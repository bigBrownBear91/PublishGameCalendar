using Microsoft.Extensions.Logging.Abstractions;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Services.Pollers;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class DoubleRoundRobinPollerTests
{
    private const string SourceUrl = "https://wpmatch.ch/team/sk-bern-ii/";

    private const string FixtureHtml = """
                                       <div class="match-fixture">
                                           <div class="team-home"><a href="https://wpmatch.ch/team/sk-bern-ii/">SK Bern II</a></div>
                                           <div class="team-away"><a href="https://wpmatch.ch/team/wbk-sm-zuerich-iii/">WBK SM Zürich III</a></div>
                                           <div class="match-date"><a href="https://wpmatch.ch/event/263129/">24.04.2026</a></div>
                                           <div class="match-time"><a href="https://wpmatch.ch/event/263129/">20h30</a></div>
                                           <div class="match-venue">Bern HB / Neufeld</div>
                                       </div>
                                       """;

    private const string ResultHtml = """
                                      <div class="match-result">
                                          <div class="team-home"><a href="https://wpmatch.ch/team/sk-bern-ii/">SK Bern II</a></div>
                                          <div class="team-away"><a href="https://wpmatch.ch/team/wk-thun/">WK Thun</a></div>
                                          <div class="match-score"><a href="https://wpmatch.ch/event/263130/">13 - 14</a></div>
                                          <div class="match-date"><a href="https://wpmatch.ch/event/263130/">06.03.2026</a></div>
                                          <div class="match-venue">Bern HB / Neufeld</div>
                                      </div>
                                      """;

    private readonly DoubleRoundRobinPoller _sut =
        new DoubleRoundRobinPoller(new HttpClient(), NullLogger<DoubleRoundRobinPoller>.Instance);

    private static string Page(string body)
    {
        return $"<html><body>{body}</body></html>";
    }

    [Fact]
    public async Task ParseAsync_Fixture_ExtractsOpponentUidTimeAndLocation()
    {
        // Act
        List<Event> events = await _sut.ParseAsync(Page(FixtureHtml), SourceUrl);

        // Assert
        Assert.Single(events);
        Event ev = events[0];
        Assert.Equal("WBK SM Zürich III", ev.Title);
        Assert.Equal("wpmatch-263129", ev.Uid);
        Assert.Equal(new DateTime(2026, 4, 24, 20, 30, 0, DateTimeKind.Utc), ev.Start);
        Assert.Equal("Bern HB / Neufeld", ev.Location);
    }

    [Fact]
    public async Task ParseAsync_Result_ExtractsOpponentWithoutScore()
    {
        // Act
        List<Event> events = await _sut.ParseAsync(Page(ResultHtml), SourceUrl);

        // Assert
        Assert.Single(events);
        Event ev = events[0];
        Assert.Equal("WK Thun", ev.Title);
        Assert.Equal("wpmatch-263130", ev.Uid);
        Assert.Equal(new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc), ev.Start);
    }

    [Fact]
    public async Task ParseAsync_WhenOurTeamIsAway_UsesHomeTeamAsOpponent()
    {
        // Arrange
        const string html = """
                            <html><body>
                            <div class="match-fixture">
                                <div class="team-home"><a href="https://wpmatch.ch/team/wk-thun/">WK Thun</a></div>
                                <div class="team-away"><a href="https://wpmatch.ch/team/sk-bern-ii/">SK Bern II</a></div>
                                <div class="match-date"><a href="https://wpmatch.ch/event/999/">01.05.2026</a></div>
                                <div class="match-time"><a href="https://wpmatch.ch/event/999/">18h00</a></div>
                                <div class="match-venue">Thun Arena</div>
                            </div>
                            </body></html>
                            """;

        // Act
        List<Event> events = await _sut.ParseAsync(html, SourceUrl);

        // Assert
        Assert.Single(events);
        Assert.Equal("WK Thun", events[0].Title);
    }

    [Fact]
    public async Task ParseAsync_MixedFixturesAndResults_ReturnsAll()
    {
        // Act
        List<Event> events = await _sut.ParseAsync(Page(FixtureHtml + ResultHtml), SourceUrl);

        // Assert
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ParseAsync_ResultWithNoTime_UsesDateAtMidnightUtc()
    {
        // Act
        List<Event> events = await _sut.ParseAsync(Page(ResultHtml), SourceUrl);

        // Assert
        Assert.Equal(TimeSpan.Zero, events[0].Start.TimeOfDay);
        Assert.Equal(DateTimeKind.Utc, events[0].Start.Kind);
    }

    [Fact]
    public async Task ParseAsync_EmptyPage_ReturnsEmptyList()
    {
        // Act
        List<Event> events = await _sut.ParseAsync("<html><body></body></html>", SourceUrl);

        // Assert
        Assert.Empty(events);
    }
}