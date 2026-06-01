using Microsoft.AspNetCore.Mvc;
using Moq;
using PublishGameCalendar.Controllers;
using PublishGameCalendar.Domain;
using PublishGameCalendar.DTOs;
using PublishGameCalendar.Repositories;
using PublishGameCalendar.Services.Enrichment;
using PublishGameCalendar.Services.Ics;
using Xunit;

namespace PublishGameCalendar.Tests.Controllers;

public class EnrichmentControllerTests
{
    private static readonly DateTime Start = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc);

    private static readonly Series TestSeries = new Series
        { Id = "s1", Name = "Test League", PollerType = "StubPoller", Enabled = true };

    private static readonly List<Event> RawEvents =
    [
        new Event { Uid = "e1", Title = "Match A", Start = Start, End = End }
    ];

    private static (
        Mock<IEnrichmentRepository> enrichmentRepo,
        Mock<IIcsService> icsService,
        Mock<IEventEnricher> eventEnricher,
        Mock<ISeriesRepository> seriesRepo,
        EnrichmentController sut) Build()
    {
        Mock<IEnrichmentRepository> enrichmentRepo = new Mock<IEnrichmentRepository>();
        Mock<IIcsService> icsService = new Mock<IIcsService>();
        Mock<IEventEnricher> eventEnricher = new Mock<IEventEnricher>();
        Mock<ISeriesRepository> seriesRepo = new Mock<ISeriesRepository>();

        eventEnricher.Setup(e => e.Merge(It.IsAny<List<Event>>(), It.IsAny<IEnumerable<EventEnrichment>>()))
            .Returns<List<Event>, IEnumerable<EventEnrichment>>((events, _) => events);

        EnrichmentController sut = new EnrichmentController(
            enrichmentRepo.Object, icsService.Object, eventEnricher.Object, seriesRepo.Object);

        return (enrichmentRepo, icsService, eventEnricher, seriesRepo, sut);
    }

    // ── GET ──

    [Fact]
    public async Task GetEnrichments_WhenSeriesExists_ReturnsEnrichmentDtos()
    {
        // Arrange
        (Mock<IEnrichmentRepository> enrichmentRepo, _, _, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync(
        [
            new EventEnrichment { SeriesId = "s1", EventUid = "e1", Description = "Final" }
        ]);

        // Act
        ActionResult<List<EnrichmentDto>> result = await sut.GetEnrichments("s1");

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        List<EnrichmentDto> dtos = Assert.IsType<List<EnrichmentDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.Equal("e1", dtos[0].EventUid);
        Assert.Equal("Final", dtos[0].Description);
    }

    [Fact]
    public async Task GetEnrichments_WhenSeriesNotFound_ReturnsNotFound()
    {
        // Arrange
        (_, _, _, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Series?)null);

        // Act
        ActionResult<List<EnrichmentDto>> result = await sut.GetEnrichments("missing");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── PUT ──

    [Fact]
    public async Task UpsertEnrichment_WhenSeriesExists_UpsertsAndReturnsNoContent()
    {
        // Arrange
        (Mock<IEnrichmentRepository> enrichmentRepo, Mock<IIcsService> icsService, _,
            Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync([]);
        icsService.Setup(s => s.ParseRawSnapshotAsync("s1")).ReturnsAsync(RawEvents);

        UpsertEnrichmentRequest request = new UpsertEnrichmentRequest { Description = "Playoff game" };

        // Act
        IActionResult result = await sut.UpsertEnrichment("s1", "e1", request);

        // Assert
        Assert.IsType<NoContentResult>(result);
        enrichmentRepo.Verify(r => r.UpsertAsync(It.Is<EventEnrichment>(e =>
            e.SeriesId == "s1" && e.EventUid == "e1" && e.Description == "Playoff game")), Times.Once);
    }

    [Fact]
    public async Task UpsertEnrichment_WhenSeriesExists_RegeneratesEnrichedIcs()
    {
        // Arrange
        (Mock<IEnrichmentRepository> enrichmentRepo, Mock<IIcsService> icsService,
            Mock<IEventEnricher> eventEnricher, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync([]);
        icsService.Setup(s => s.ParseRawSnapshotAsync("s1")).ReturnsAsync(RawEvents);

        // Act
        await sut.UpsertEnrichment("s1", "e1", new UpsertEnrichmentRequest { Description = "Note" });

        // Assert — enricher called then ICS written
        eventEnricher.Verify(e => e.Merge(RawEvents, It.IsAny<IEnumerable<EventEnrichment>>()), Times.Once);
        icsService.Verify(s => s.WriteAsync("s1", "Test League", It.IsAny<List<Event>>()), Times.Once);
    }

    [Fact]
    public async Task UpsertEnrichment_WhenRawSnapshotIsEmpty_DoesNotWriteIcs()
    {
        // Arrange — no poll has run yet; raw snapshot is empty
        (_, Mock<IIcsService> icsService, _, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        icsService.Setup(s => s.ParseRawSnapshotAsync("s1")).ReturnsAsync([]);

        // Act
        IActionResult result = await sut.UpsertEnrichment("s1", "e1", new UpsertEnrichmentRequest { Description = "Note" });

        // Assert — enrichment saved but ICS not regenerated (no raw data to merge with)
        Assert.IsType<NoContentResult>(result);
        icsService.Verify(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<Event>>()), Times.Never);
    }

    [Fact]
    public async Task UpsertEnrichment_WhenSeriesNotFound_ReturnsNotFound()
    {
        // Arrange
        (_, _, _, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Series?)null);

        // Act
        IActionResult result = await sut.UpsertEnrichment("missing", "e1", new UpsertEnrichmentRequest());

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    // ── DELETE ──

    [Fact]
    public async Task DeleteEnrichment_WhenSeriesExists_DeletesAndReturnsNoContent()
    {
        // Arrange
        (Mock<IEnrichmentRepository> enrichmentRepo, Mock<IIcsService> icsService, _,
            Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync([]);
        icsService.Setup(s => s.ParseRawSnapshotAsync("s1")).ReturnsAsync(RawEvents);

        // Act
        IActionResult result = await sut.DeleteEnrichment("s1", "e1");

        // Assert
        Assert.IsType<NoContentResult>(result);
        enrichmentRepo.Verify(r => r.DeleteAsync("s1", "e1"), Times.Once);
    }

    [Fact]
    public async Task DeleteEnrichment_WhenSeriesExists_RegeneratesEnrichedIcs()
    {
        // Arrange
        (Mock<IEnrichmentRepository> enrichmentRepo, Mock<IIcsService> icsService,
            Mock<IEventEnricher> eventEnricher, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("s1")).ReturnsAsync(TestSeries);
        enrichmentRepo.Setup(r => r.GetBySeriesIdAsync("s1")).ReturnsAsync([]);
        icsService.Setup(s => s.ParseRawSnapshotAsync("s1")).ReturnsAsync(RawEvents);

        // Act
        await sut.DeleteEnrichment("s1", "e1");

        // Assert
        icsService.Verify(s => s.WriteAsync("s1", "Test League", It.IsAny<List<Event>>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEnrichment_WhenSeriesNotFound_ReturnsNotFound()
    {
        // Arrange
        (_, _, _, Mock<ISeriesRepository> seriesRepo, EnrichmentController sut) = Build();
        seriesRepo.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Series?)null);

        // Act
        IActionResult result = await sut.DeleteEnrichment("missing", "e1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
