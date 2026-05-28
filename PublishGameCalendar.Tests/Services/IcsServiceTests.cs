using Microsoft.Extensions.Configuration;
using PublishGameCalendar.Domain;
using PublishGameCalendar.Services.Ics;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class IcsServiceTests : IDisposable
{
    private readonly IcsService _sut;
    private readonly string _tempDir;

    public IcsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["IcsFilesPath"] = _tempDir })
            .Build();

        _sut = new IcsService(config);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ParseAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange — no file written

        // Act
        List<Event> result = await _sut.ParseAsync("s-none");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task WriteAsync_ThenParseAsync_RoundTripsEvents()
    {
        // Arrange
        List<Event> events = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            },
            new Event
            {
                Uid = "uid-2", Title = "Match B", Start = new DateTime(2026, 5, 8, 18, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        await _sut.WriteAsync("s1", "Series One", events);
        List<Event> parsed = await _sut.ParseAsync("s1");

        // Assert
        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, e => e.Uid == "uid-1" && e.Title == "Match A");
        Assert.Contains(parsed, e => e.Uid == "uid-2" && e.Title == "Match B");
    }

    [Fact]
    public async Task WriteAsync_SetsCalendarNameProperty()
    {
        // Arrange
        List<Event> events = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        await _sut.WriteAsync("s-name", "Swiss League 2025/26", events);

        // Assert
        string content = File.ReadAllText(_sut.GetIcsFilePath("s-name"));
        Assert.Contains("X-WR-CALNAME:Swiss League 2025/26", content);
    }

    [Fact]
    public async Task DiffAsync_WhenFreshEventsAreIdentical_ReturnsNoDiff()
    {
        // Arrange
        List<Event> events = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };
        await _sut.WriteAsync("s2", "Series Two", events);

        // Act
        EventDiff diff = await _sut.DiffAsync("s2", events);

        // Assert
        Assert.False(diff.HasChanges);
    }

    [Fact]
    public async Task DiffAsync_WhenEventIsAdded_ReportsAddition()
    {
        // Arrange
        List<Event> existing = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };
        await _sut.WriteAsync("s3", "Series Three", existing);

        List<Event> fresh = existing.Concat(new[]
        {
            new Event
            {
                Uid = "uid-2", Title = "Match B", Start = new DateTime(2026, 5, 8, 18, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc)
            }
        }).ToList();

        // Act
        EventDiff diff = await _sut.DiffAsync("s3", fresh);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.Single(diff.Added);
        Assert.Equal("uid-2", diff.Added[0].Uid);
    }

    [Fact]
    public async Task DiffAsync_WhenEventIsRemoved_ReportsDeletion()
    {
        // Arrange
        List<Event> existing = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            },
            new Event
            {
                Uid = "uid-2", Title = "Match B", Start = new DateTime(2026, 5, 8, 18, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc)
            }
        };
        await _sut.WriteAsync("s4", "Series Four", existing);

        List<Event> fresh = existing.Take(1).ToList();

        // Act
        EventDiff diff = await _sut.DiffAsync("s4", fresh);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.Single(diff.Removed);
        Assert.Equal("uid-2", diff.Removed[0].Uid);
    }

    [Fact]
    public async Task DiffAsync_WhenEventStartTimeChanges_ReportsModification()
    {
        // Arrange
        List<Event> existing = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };
        await _sut.WriteAsync("s5", "Series Five", existing);

        List<Event> fresh = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 16, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        EventDiff diff = await _sut.DiffAsync("s5", fresh);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.Single(diff.Modified);
    }

    [Fact]
    public async Task DiffAsync_WhenNoExistingFile_AllFreshEventsAreAdded()
    {
        // Arrange — no prior file
        List<Event> fresh = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        EventDiff diff = await _sut.DiffAsync("s6", fresh);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.Single(diff.Added);
    }
}
