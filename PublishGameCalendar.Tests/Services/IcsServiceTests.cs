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

    // ── Raw snapshot ──

    [Fact]
    public async Task WriteRawSnapshotAsync_ThenParseRawSnapshotAsync_RoundTripsEvents()
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
        await _sut.WriteRawSnapshotAsync("sr1", events);
        List<Event> parsed = await _sut.ParseRawSnapshotAsync("sr1");

        // Assert
        Assert.Single(parsed);
        Assert.Equal("uid-1", parsed[0].Uid);
    }

    [Fact]
    public async Task WriteRawSnapshotAsync_DoesNotWriteCalendarNameProperty()
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
        await _sut.WriteRawSnapshotAsync("sr2", events);

        // Assert — raw file must not contain X-WR-CALNAME (not user-visible)
        string rawPath = Path.Combine(_tempDir, "sr2_raw.ics");
        string content = File.ReadAllText(rawPath);
        Assert.DoesNotContain("X-WR-CALNAME", content);
    }

    [Fact]
    public async Task ParseRawSnapshotAsync_WhenNoFileExists_ReturnsEmptyList()
    {
        // Arrange — no file written

        // Act
        List<Event> result = await _sut.ParseRawSnapshotAsync("sr-none");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiffRawAsync_WhenNoRawSnapshot_AllFreshEventsAreAdded()
    {
        // Arrange — no raw snapshot on disk
        List<Event> fresh = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        EventDiff diff = await _sut.DiffRawAsync("sr3", fresh);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.Single(diff.Added);
    }

    [Fact]
    public async Task DiffRawAsync_ComparesAgainstRawSnapshotNotEnrichedFile()
    {
        // Arrange — raw snapshot has event without description; enriched .ics has description added
        List<Event> rawEvents = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc), Description = null
            }
        };
        await _sut.WriteRawSnapshotAsync("sr4", rawEvents);

        // Enriched .ics has the same event but with admin-added description
        List<Event> enrichedEvents = new List<Event>
        {
            new Event
            {
                Uid = "uid-1", Title = "Match A", Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc), Description = "Admin note"
            }
        };
        await _sut.WriteAsync("sr4", "Series", enrichedEvents);

        // Fresh poll returns same data as raw (no change on website)
        List<Event> freshFromWebsite = rawEvents;

        // Act — diff raw should report no changes (website didn't change)
        EventDiff diff = await _sut.DiffRawAsync("sr4", freshFromWebsite);

        // Assert
        Assert.False(diff.HasChanges);
    }

    [Fact]
    public async Task DeleteFilesAsync_RemovesBothEnrichedAndRawFiles()
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
        await _sut.WriteAsync("sd1", "Series", events);
        await _sut.WriteRawSnapshotAsync("sd1", events);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sd1.ics")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sd1_raw.ics")));

        // Act
        await _sut.DeleteFilesAsync("sd1");

        // Assert
        Assert.False(File.Exists(Path.Combine(_tempDir, "sd1.ics")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "sd1_raw.ics")));
    }

    [Fact]
    public async Task DeleteFilesAsync_WhenFilesDoNotExist_DoesNotThrow()
    {
        // Act & Assert — should not throw
        await _sut.DeleteFilesAsync("sd-missing");
    }
}
