using Microsoft.Extensions.Logging.Abstractions;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Services.Pollers;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class Poller1Tests
{
    private const string SourceUrl = "https://wpmatch.ch/team/sk-bern-ii/";

    // tr rows for composition; wrap with TablePage() before parsing
    private const string FixtureRow = """
        <tr class="sp-row sp-post">
          <td>
            <span class="team-logo logo-odd"><a href="https://wpmatch.ch/team/sk-bern-ii/"><div class="team-name">SK Bern II</div></a></span>
            <span class="team-logo logo-even"><a href="https://wpmatch.ch/team/wbk-sm-zuerich-iii/"><div class="team-name">WBK SM Zürich III</div></a></span>
            <time class="sp-event-date" datetime="2026-04-24 20:30:00" content="2026-04-24T20:30:00+02:00">
              <a href="https://wpmatch.ch/event/263129/">24.04.2026</a>
            </time>
            <div class="sp-event-venue"><div itemprop="address">Bern HB / Neufeld</div></div>
            <div style="display:none;" class="sp-event-venue"><div itemprop="address">N/A</div></div>
          </td>
        </tr>
        """;

    private const string ResultRow = """
        <tr class="sp-row sp-post alternate">
          <td>
            <span class="team-logo logo-odd"><a href="https://wpmatch.ch/team/sk-bern-ii/"><div class="team-name">SK Bern II</div></a></span>
            <span class="team-logo logo-even"><a href="https://wpmatch.ch/team/wk-thun/"><div class="team-name">WK Thun</div></a></span>
            <time class="sp-event-date" datetime="2026-03-06 20:00:00" content="2026-03-06T20:00:00+01:00">
              <a href="https://wpmatch.ch/event/263130/">06.03.2026</a>
            </time>
            <h5 class="sp-event-results"><a href="https://wpmatch.ch/event/263130/"><span class="sp-result">13 - 14</span></a></h5>
            <div class="sp-event-venue"><div itemprop="address">Bern HB / Neufeld</div></div>
          </td>
        </tr>
        """;

    private readonly Poller1 _sut =
        new Poller1(new HttpClient(), NullLogger<Poller1>.Instance);

    private static string TablePage(string rows) =>
        $"<html><body><table><tbody>{rows}</tbody></table></body></html>";

    [Fact]
    public async Task ParseAsync_HomeFixture_ExtractsOpponentUidTimeAndLocation()
    {
        List<Event> events = await _sut.ParseAsync(TablePage(FixtureRow), SourceUrl);

        Assert.Single(events);
        Event ev = events[0];
        Assert.Equal("WBK SM Zürich III", ev.Title);
        Assert.Equal("wpmatch-263129", ev.Uid);
        Assert.Equal(new DateTime(2026, 4, 24, 18, 30, 0, DateTimeKind.Utc), ev.Start);
        Assert.Equal("Bern HB / Neufeld", ev.Location);
    }

    [Fact]
    public async Task ParseAsync_Result_ExtractsOpponentAndConvertsOffsetToUtc()
    {
        List<Event> events = await _sut.ParseAsync(TablePage(ResultRow), SourceUrl);

        Assert.Single(events);
        Event ev = events[0];
        Assert.Equal("WK Thun", ev.Title);
        Assert.Equal("wpmatch-263130", ev.Uid);
        Assert.Equal(new DateTime(2026, 3, 6, 19, 0, 0, DateTimeKind.Utc), ev.Start);
    }

    [Fact]
    public async Task ParseAsync_WhenOurTeamIsAway_UsesHomeTeamAsOpponent()
    {
        const string html = """
            <html><body><table><tbody>
            <tr class="sp-row sp-post">
              <td>
                <span class="team-logo logo-odd"><a href="https://wpmatch.ch/team/wk-thun/"><div class="team-name">WK Thun</div></a></span>
                <span class="team-logo logo-even"><a href="https://wpmatch.ch/team/sk-bern-ii/"><div class="team-name">SK Bern II</div></a></span>
                <time class="sp-event-date" datetime="2026-05-01 18:00:00" content="2026-05-01T18:00:00+02:00">
                  <a href="https://wpmatch.ch/event/999/">01.05.2026</a>
                </time>
                <div class="sp-event-venue"><div itemprop="address">Thun Arena</div></div>
              </td>
            </tr>
            </tbody></table></body></html>
            """;

        List<Event> events = await _sut.ParseAsync(html, SourceUrl);

        Assert.Single(events);
        Assert.Equal("WK Thun", events[0].Title);
    }

    [Fact]
    public async Task ParseAsync_MixedFixturesAndResults_ReturnsAll()
    {
        List<Event> events = await _sut.ParseAsync(TablePage(FixtureRow + ResultRow), SourceUrl);

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ParseAsync_MissingContentAttribute_FallsBackToDatetimeAttribute()
    {
        const string html = """
            <html><body><table><tbody>
            <tr class="sp-row sp-post">
              <td>
                <span class="team-logo logo-odd"><a href="https://wpmatch.ch/team/sk-bern-ii/"><div class="team-name">SK Bern II</div></a></span>
                <span class="team-logo logo-even"><a href="https://wpmatch.ch/team/wk-thun/"><div class="team-name">WK Thun</div></a></span>
                <time class="sp-event-date" datetime="2026-06-15 20:00:00">
                  <a href="https://wpmatch.ch/event/777/">15.06.2026</a>
                </time>
              </td>
            </tr>
            </tbody></table></body></html>
            """;

        List<Event> events = await _sut.ParseAsync(html, SourceUrl);

        Assert.Single(events);
        Assert.Equal(new DateTime(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc), events[0].Start);
    }

    [Fact]
    public async Task ParseAsync_WhenVenueIsNA_ReturnsNullLocation()
    {
        const string html = """
            <html><body><table><tbody>
            <tr class="sp-row sp-post">
              <td>
                <span class="team-logo logo-odd"><a href="https://wpmatch.ch/team/sk-bern-ii/"><div class="team-name">SK Bern II</div></a></span>
                <span class="team-logo logo-even"><a href="https://wpmatch.ch/team/wk-thun/"><div class="team-name">WK Thun</div></a></span>
                <time class="sp-event-date" datetime="2026-06-15 20:00:00" content="2026-06-15T20:00:00+02:00">
                  <a href="https://wpmatch.ch/event/888/">15.06.2026</a>
                </time>
                <div class="sp-event-venue"><div itemprop="address">N/A</div></div>
              </td>
            </tr>
            </tbody></table></body></html>
            """;

        List<Event> events = await _sut.ParseAsync(html, SourceUrl);

        Assert.Single(events);
        Assert.Null(events[0].Location);
    }

    [Fact]
    public async Task ParseAsync_EmptyPage_ReturnsEmptyList()
    {
        List<Event> events = await _sut.ParseAsync("<html><body></body></html>", SourceUrl);

        Assert.Empty(events);
    }
}
