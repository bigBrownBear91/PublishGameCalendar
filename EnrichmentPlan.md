# Enrichment Feature — Implementation Plan

## Goal

Admins can enrich individual calendar events (Title, Location, Description) with data that
persists across polls. The website remains master: if the website provides a non-empty value
for a field, it overrides any enrichment. If the website provides nothing for a field,
the enrichment value is preserved.

---

## Key Decisions

| Decision | Reason |
|---|---|
| Raw snapshot `{seriesId}_raw.ics` for diffing | Prevents false-positive diffs from enriched data in the public .ics |
| Enrichments stored in DynamoDB `event_enrichments` | Enrichment is state, not output; survives .ics regeneration |
| `EventEnricher` as a pure class | Merge logic is domain logic, not file I/O; trivially testable without mocks |
| `EnrichmentController` independent of `AdminController` | Not coupled to admin; role policy applied at the attribute level |
| Orphans deleted by Orchestrator after each poll | Keeps enrichment table consistent with live events automatically |
| EnrichmentController regenerates enriched .ics immediately | Admin sees changes reflected in calendar without waiting for next poll |

---

## Merge Rule

For each enrichable field (`Title`, `Location`, `Description`) per event:
- Polled value is **non-null and non-empty** → use polled value (enrichment ignored)
- Polled value is **null or empty** → use enrichment value if one exists, otherwise keep empty

`Start` and `End` are never enrichable.

---

## Data Model

### New DynamoDB table: `event_enrichments`

| Attribute | Type | Key |
|---|---|---|
| `series_id` | string | Hash key |
| `event_uid` | string | Range key |
| `title` | string? | — |
| `location` | string? | — |
| `description` | string? | — |

### New domain class: `EventEnrichment`

```csharp
// Domain/EventEnrichment.cs
public class EventEnrichment
{
    public string SeriesId { get; set; }
    public string EventUid { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}
```

---

## New Files

### 1. `Domain/EventEnrichment.cs`
Domain model above. DynamoDB mapping attributes applied.

### 2. `Services/Enrichment/IEventEnricher.cs`
```csharp
public interface IEventEnricher
{
    List<Event> Merge(List<Event> polledEvents, IEnumerable<EventEnrichment> enrichments);
}
```

### 3. `Services/Enrichment/EventEnricher.cs`
Pure class. No repository dependencies.
- Builds a `Dictionary<string, EventEnrichment>` keyed by `EventUid`
- For each polled event: applies merge rule per field
- Returns new `List<Event>` (does not mutate inputs)

### 4. `Repositories/IEnrichmentRepository.cs`
```csharp
public interface IEnrichmentRepository
{
    Task<List<EventEnrichment>> GetBySeriesIdAsync(string seriesId);
    Task UpsertAsync(EventEnrichment enrichment);
    Task DeleteAsync(string seriesId, string eventUid);
    Task DeleteAllBySeriesIdAsync(string seriesId);
}
```

### 5. `Repositories/DynamoDbEnrichmentRepository.cs`
Implements `IEnrichmentRepository` via `IDynamoDbContext`.
- `GetBySeriesIdAsync`: query by hash key `series_id`
- `UpsertAsync`: `SaveAsync`
- `DeleteAsync`: `DeleteAsync` by composite key
- `DeleteAllBySeriesIdAsync`: `GetBySeriesIdAsync` then delete each item

### 6. `DTOs/EnrichmentDto.cs`
Response DTO returned by `GET /api/enrichments/{seriesId}`.
Fields: `EventUid`, `Title?`, `Location?`, `Description?`.

### 7. `DTOs/UpsertEnrichmentRequest.cs`
Request DTO for `PUT /api/enrichments/{seriesId}/{eventUid}`.
Fields: `Title?`, `Location?`, `Description?`.

### 8. `Controllers/EnrichmentController.cs`

Route: `api/enrichments`, `[Authorize(Roles = "Admin")]`

| Method | Route | Action |
|---|---|---|
| `GET` | `/{seriesId}` | Return all enrichments for the series |
| `PUT` | `/{seriesId}/{eventUid}` | Upsert enrichment, then regenerate enriched .ics |
| `DELETE` | `/{seriesId}/{eventUid}` | Delete enrichment, then regenerate enriched .ics |

The PUT and DELETE handlers regenerate the enriched `.ics` immediately by:
1. `icsService.ParseRawSnapshotAsync(seriesId)` → raw events
2. `enrichmentRepo.GetBySeriesIdAsync(seriesId)` → enrichments
3. `eventEnricher.Merge(rawEvents, enrichments)` → enriched events
4. `icsService.WriteAsync(seriesId, seriesName, enrichedEvents)`

Dependencies injected: `IEnrichmentRepository`, `IIcsService`, `IEventEnricher`, `ISeriesRepository`.

---

## Modified Files

### 9. `Services/Ics/IIcsService.cs` — add four methods

```csharp
Task<List<Event>> ParseRawSnapshotAsync(string seriesId);
Task WriteRawSnapshotAsync(string seriesId, List<Event> events);
Task<EventDiff> DiffRawAsync(string seriesId, List<Event> freshEvents);
Task DeleteFilesAsync(string seriesId);  // deletes both .ics and _raw.ics
```

### 10. `Services/Ics/IcsService.cs` — implement new methods

- `GetRawSnapshotFilePath`: returns `{icsFilesPath}/{seriesId}_raw.ics`
- `ParseRawSnapshotAsync`: like `ParseAsync` but reads `_raw.ics`
- `WriteRawSnapshotAsync`: like `WriteAsync` but writes `_raw.ics`, no `X-WR-CALNAME`
- `DiffRawAsync`: like `DiffAsync` but reads from `_raw.ics` as baseline
- `DeleteFilesAsync`: `File.Delete` for both files (if they exist)
- Extract shared diff logic into a private `ComputeDiff(List<Event> existing, List<Event> fresh)` helper
  to avoid duplication between `DiffAsync` and `DiffRawAsync`

### 11. `Services/Orchestrator/OrchestratorService.cs` — updated poll pipeline

New dependencies: `IEnrichmentRepository`, `IEventEnricher` (resolved from scope).

Replace the current poll body:

```
OLD:
  diff = icsService.DiffAsync(series.Id, freshEvents)
  if (diff.HasChanges) icsService.WriteAsync(...)

NEW:
  diff = icsService.DiffRawAsync(series.Id, freshEvents)
  icsService.WriteRawSnapshotAsync(series.Id, freshEvents)        // always
  enrichments = enrichmentRepo.GetBySeriesIdAsync(series.Id)
  orphanUids = enrichments whose EventUid is not in freshEvents
  foreach orphan: enrichmentRepo.DeleteAsync(series.Id, uid)      // cleanup
  enrichedEvents = eventEnricher.Merge(freshEvents, enrichments)
  if (diff.HasChanges) icsService.WriteAsync(series.Id, series.Name, enrichedEvents)
```

The raw snapshot write is always performed on a successful poll (non-zero events), regardless
of whether changes were detected, so it stays in sync with the current website state.

### 12. `Controllers/AdminController.cs` — series delete cleanup

Add `IEnrichmentRepository` and `IIcsService` constructor dependencies.

In `DeleteSeries`:
```csharp
await enrichmentRepo.DeleteAllBySeriesIdAsync(id);
await icsService.DeleteFilesAsync(id);
await seriesRepo.DeleteAsync(id);
```

### 13. `Program.cs` — register new services

```csharp
builder.Services.AddScoped<IEnrichmentRepository, DynamoDbEnrichmentRepository>();
builder.Services.AddScoped<IEventEnricher, EventEnricher>();
```

---

## Test Files

| File | What it tests |
|---|---|
| `Tests/Services/EventEnricherTests.cs` | Merge rules: website wins when non-empty; enrichment persists when website empty; Start/End never overridden; null enrichment passthrough |
| `Tests/Repositories/EnrichmentRepositoryTests.cs` | GetBySeriesId, Upsert, Delete, DeleteAllBySeriesId (using mock IDynamoDbContext) |
| `Tests/Services/IcsServiceTests.cs` | Add tests for `ParseRawSnapshotAsync`, `WriteRawSnapshotAsync`, `DiffRawAsync`, `DeleteFilesAsync` |
| `Tests/Services/OrchestratorServiceTests.cs` | Updated: Orchestrator uses DiffRawAsync; verifies raw snapshot written; verifies orphan enrichments deleted; verifies merged events passed to WriteAsync |

---

## Execution Order

1. `EventEnrichment` domain model
2. `IEventEnricher` interface + `EventEnricher` implementation + tests
3. `IEnrichmentRepository` interface + `DynamoDbEnrichmentRepository` + tests
4. `IIcsService` additions + `IcsService` implementation + tests
5. `OrchestratorService` update + tests
6. DTOs + `EnrichmentController`
7. `AdminController` delete cleanup
8. `Program.cs` DI wiring
