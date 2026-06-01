# Idea of app
The app should poll a website for events. These events should then be put into a .ics-file and hosted, such that everybody can subscribe with a URL and see the events in his calendar. The app should poll on a regular base and if events are changed, a notification should be sent to every user, who subscribed to the notifications.
There can be multiple series of events, for each series of events, a .ics-file is generated.

# Requirements
- URLs for calendars, which can be imported in one's own calendar are offered. The user doesn't need to register for that
- Users can also subscribe to notifications, when an event of a serie is changed. For that, a registration is needed and the user need to be logged in
- Users can unsubscribe from notifications, if they're logged in
- Users see in a dashboard, which series exist and to which they can subscribe or unsubscribe
- An admin user can see, which registered users exist and to which series they're subscribed for notifications
- The app is polling the website in a frequency defined in the settings. The admin can change the frequency, stop the updates altogether and also delete a serie
- The admin can enrich individual events with additional metadata (Title, Location, Description) that persists across polls

# Techstack
- The application runs as a single Docker container deployed on EC2 (t4g.micro ARM, eu-west-1)
- The webserver is written in C# and .NET
- The database is AWS DynamoDB (PAY_PER_REQUEST); no DB server to manage
- The .ics-files are stored on a persistent EBS volume mounted at `/data/ics`
- The frontend consists of simple HTML/JS files, hosted and served by the webserver
- Authentication is settings-based: admin credentials are stored in appsettings/environment variables and validated to issue a JWT

# Coding guidelines
- The code is written in an object oriented fashion and adheres to basic principles as SOLID and DRY. Low coupling and high cohesion are maintained
- Unittests are written for every method. They follow the pattern Arrange-Act-Assert
- Names of classes and methods are clear and precise and are telling, what the responsability of this code is
- A strict separation of settings and code is maintained

# Workflow
- Don't assume, ask for clarification in case of ambiguity
- Before coding, make an execution plan and ask for approval
- Stick to test-driven programming
- Review your changes before concluding a task

# Architecture

## System Architecture

One container, deployed on EC2:

- **C# App** — REST API, static HTML/JS, .ics file serving, Orchestrator, Pollers, ICS Service, Enrichment Service

Two persistent stores:
- **DynamoDB** — series definitions, polling config, event enrichments
- **EBS volume** — `.ics` files (enriched, user-facing) and `_raw.ics` snapshots (internal, never served)

## C# App — Internal Components

### REST API Layer (ASP.NET Core)
- **Auth Middleware** — validates JWT, attaches role (Anonymous / Admin) to every request
- **SeriesController** — public: list series with .ics URLs; serves enriched `.ics` files without authentication
- **AuthController** — login with settings-based credentials, returns JWT
- **AdminController** — series CRUD, polling config; requires Admin role; deletes enrichments and ICS files on series delete
- **EnrichmentController** — per-event enrichment CRUD (`GET/PUT/DELETE /api/enrichments/{seriesId}/{eventUid}`); requires Admin role; regenerates enriched `.ics` immediately on change
- **Static File Middleware** — serves HTML/JS frontend

### Orchestrator (IHostedService background service)
Runs on a schedule read from the database. For each active series:
1. Resolve the correct poller via `PollerFactory`
2. Fetch fresh events via `IWebsitePoller`
3. Diff fresh events against the raw snapshot (`IcsService.DiffRawAsync`)
4. Write updated raw snapshot (`IcsService.WriteRawSnapshotAsync`) — always, regardless of changes
5. Delete orphan enrichments (events no longer returned by the website)
6. Merge fresh events with stored enrichments (`EventEnricher.Merge`)
7. If changes detected:
   a. Write updated enriched `.ics` file to volume (`IcsService.WriteAsync`)
   b. Set `LastChangeAt` timestamp

The REST API can configure the Orchestrator (start/stop, change frequency) but does not invoke it directly.

### Poller Layer
- **`IWebsitePoller` interface** — single method: `FetchEventsAsync(series) → List<Event>`
- **Concrete implementations** — one class per source website structure (HTML scraping)
- **`PollerFactory`** — resolves the correct `IWebsitePoller` implementation for a given series

Pollers are kept inside the C# App container. The `IWebsitePoller` interface ensures adding a new poller is a new class only — no modification of existing code (Open/Closed).

### ICS Service
Two files per series on the EBS volume:
- **`{seriesId}.ics`** — enriched, user-facing calendar served to subscribers
- **`{seriesId}_raw.ics`** — pure website data, never served; used as the change-detection baseline

Methods:
- `ParseAsync` / `WriteAsync` — read/write the enriched `.ics`
- `ParseRawSnapshotAsync` / `WriteRawSnapshotAsync` — read/write the raw snapshot
- `DiffRawAsync` — diff fresh polled events against the raw snapshot (avoids false positives from enrichment)
- `DeleteFilesAsync` — deletes both files (called on series delete)

The enriched `.ics` is the canonical view for subscribers. The raw snapshot is the canonical baseline for change detection.

### Enrichment Layer
- **`EventEnrichment`** — domain model; stored in DynamoDB `event_enrichments` (hash: `series_id`, range: `event_uid`)
- **`IEventEnricher` / `EventEnricher`** — pure stateless class; applies merge rule per field: website value wins when non-empty; enrichment value persists when website field is null/empty. `Start` and `End` are never enrichable.
- **`IEnrichmentRepository` / `DynamoDbEnrichmentRepository`** — CRUD for enrichments

Enrichable fields: `Title`, `Location`, `Description`.

### Repository Layer
- **`SeriesRepository`** — series definitions (name, source URL, poller type, enabled flag)
- **`PollingConfigRepository`** — polling interval per series, last polled timestamp, enabled/disabled
- **`EnrichmentRepository`** — per-event enrichment data

All repositories talk to DynamoDB via `IDynamoDbContext` (a wrapper around `DynamoDBContext` that enables mocking in tests). Navigation properties (e.g. `PollingConfig.Series`) are populated manually — DynamoDB has no joins.

## Database Schema

DynamoDB tables (eu-west-1, PAY_PER_REQUEST):

```
series
  id (String, hash key)
  name, source_url, poller_type, enabled, created_at

polling_config
  series_id (String, hash key)  →  series.id
  interval_hours, last_polled_at, last_change_at, last_poll_failed, last_event_count, enabled

event_enrichments
  series_id (String, hash key)  →  series.id
  event_uid (String, range key)
  title?, location?, description?
```

## Key Architectural Decisions

| Decision | Reason |
|---|---|
| Single container, DynamoDB instead of PostgreSQL | EC2 t4g.micro + DynamoDB free tier covers this app's scale; no DB server to manage |
| Settings-based admin auth | No user management needed for the current feature set; avoids Identity dependency |
| `IDynamoDbContext` interface wrapping `DynamoDBContext` | `DynamoDBContext` has no built-in interface; wrapper enables `Mock<IDynamoDbContext>` in tests |
| Series IDs are `string` (GUID) | DynamoDB hash keys work better as strings |
| Raw snapshot `{seriesId}_raw.ics` for diffing | Enriched `.ics` must not be the diff baseline — enrichment would cause false-positive change detection on every poll |
| Enrichments stored in DynamoDB, not in the `.ics` file | Avoids circular read-from-own-output; ICS files are output only; enrichment survives `.ics` regeneration |
| `EventEnricher` as a pure stateless class | Merge logic is domain logic, not file I/O; no dependencies, trivially testable |
| `EnrichmentController` separate from `AdminController` | Not coupled to admin role specifically; auth enforced at attribute level; ready for future role expansion |
| Orphan enrichments deleted by Orchestrator after each poll | Keeps enrichment table consistent with live events automatically |
| Single DynamoDB query for enrichments per poll cycle | Reused for both orphan cleanup and merge step — avoids double-query |
| Orchestrator is `IHostedService`, not called by REST API | Polling must run on a schedule independent of HTTP traffic |
| `IWebsitePoller` interface with concrete implementations per source | Open/Closed: new source = new class, no existing code modified |
| `.ics` file is the event state, not the DB | Avoids data duplication; the enriched file is what subscribers see; the raw file is the change baseline |
