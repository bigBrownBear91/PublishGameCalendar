using PublishGameCalendar.Domain;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class EventDiffTests
{
    [Fact]
    public void HasChanges_WhenEmpty_ReturnsFalse()
    {
        // Arrange
        EventDiff diff = new EventDiff();

        // Assert
        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void HasChanges_WhenAddedIsNonEmpty_ReturnsTrue()
    {
        // Arrange
        EventDiff diff = new EventDiff { Added = [new Event { Uid = "x" }] };

        // Assert
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void BuildSummary_WithAllChangeTypes_FormatsCorrectly()
    {
        // Arrange
        EventDiff diff = new EventDiff
        {
            Added = [new Event(), new Event()],
            Removed = [new Event()],
            Modified = [new Event(), new Event(), new Event()]
        };

        // Act
        string summary = diff.BuildSummary();

        // Assert
        Assert.Equal("2 added, 1 removed, 3 modified", summary);
    }

    [Fact]
    public void BuildSummary_WithOnlyAdditions_OmitsOtherParts()
    {
        // Arrange
        EventDiff diff = new EventDiff { Added = [new Event()] };

        // Act
        string summary = diff.BuildSummary();

        // Assert
        Assert.Equal("1 added", summary);
    }
}