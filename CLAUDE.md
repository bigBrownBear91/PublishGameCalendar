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

# Techstack
- The application uses docker containers and a microservice-architecture
- The webserver itself is written in C# and .NET
- The database and the .ics-files are in a persistent storage
- The database is postgres sql
- The frontend consists of simple html-pages and javascript files, hosted and served by the webserver

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

## System Architecture (Containers)

Four containers in total:

1. **C# App** — REST API, static HTML/JS, .ics file serving, Orchestrator, Pollers, ICS Service, Queue Adapter
2. **PostgreSQL** — persistent relational data (Identity tables, series, subscriptions, polling config)
3. **RabbitMQ** — message queue between C# App and Notification Service (no persistence needed)
4. **Notification Service** — consumes from RabbitMQ, dispatches notifications (Mail now, extensible for other channels later)

Two persistent volumes:
- PostgreSQL data volume
- .ics files volume (mounted by C# App only; files are served via the REST API, not exposed directly)

## C# App — Internal Components

### REST API Layer (ASP.NET Core)
- **Auth Middleware** — validates JWT, attaches role (Anonymous / User / Admin) to every request
- **SeriesController** — public: list series with .ics URLs; serves .ics files without authentication
- **AuthController** — register, login, returns JWT
- **SubscriptionController** — subscribe/unsubscribe; requires logged-in role
- **AdminController** — user list, role promotion, series CRUD, polling config; requires Admin role
- **Static File Middleware** — serves HTML/JS frontend

### Orchestrator (IHostedService background service)
Runs on a schedule read from the database. For each active series:
1. Resolve the correct poller via `PollerFactory`
2. Fetch fresh events via `IWebsitePoller`
3. Diff fresh events against the existing `.ics` file via `IcsService`
4. If changes detected:
   a. Write updated `.ics` file to volume (`IcsService`)
   b. Resolve subscribers from DB (`SubscriptionRepository`)
   c. Push notification message to queue (`QueueAdapter`)

The REST API can configure the Orchestrator (start/stop, change frequency) but does not invoke it directly.

### Poller Layer
- **`IWebsitePoller` interface** — single method: `FetchEvents(seriesConfig) → List<Event>`
- **Concrete implementations** — one class per source website structure (HTML scraping)
- **`PollerFactory`** — resolves the correct `IWebsitePoller` implementation for a given series

Pollers are kept inside the C# App container (not separate containers). The `IWebsitePoller` interface ensures adding a new poller is a new class only — no modification of existing code (Open/Closed). If zero-downtime poller deployment becomes a requirement in the future, the interface contract is already compatible with a network-based (HTTP/gRPC) approach.

### ICS Service
- Parses existing `.ics` files from volume
- Diffs parsed events against freshly scraped events to detect additions, deletions, and date/time changes
- Writes updated `.ics` files to volume
- The `.ics` file is the canonical event state — events are NOT stored in the database

### Queue Adapter
- `IQueueAdapter` interface with a RabbitMQ implementation
- Keeps the Orchestrator decoupled from the queue technology
- The message includes: series name, change summary, and the resolved list of recipient email addresses (so the Notification Service needs no DB access)

### Repository Layer
- **`UserManager<ApplicationUser>`** and **`RoleManager<IdentityRole>`** — from ASP.NET Core Identity; replaces a custom UserRepository entirely
- **`SeriesRepository`** — series definitions (name, source URL, poller type, enabled flag)
- **`SubscriptionRepository`** — which user is subscribed to which series
- **`PollingConfigRepository`** — polling interval per series, last polled timestamp, enabled/disabled

All repositories talk to PostgreSQL via EF Core.

## User Management (ASP.NET Core Identity)

Uses ASP.NET Core Identity throughout — no custom user management code.

- `ApplicationUser : IdentityUser` — extend only if custom fields are needed
- `ApplicationDbContext : IdentityDbContext<ApplicationUser>`
- Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc.) are managed by EF Core migrations alongside custom tables
- Roles: `"Admin"` and `"User"`
- **First registered user automatically receives the Admin role**; any admin can promote other users
- Password hashing, credential validation, and token generation are handled by Identity APIs (`UserManager`, `SignInManager`)
- Login returns a JWT containing `sub`, `email`, and `role` claims

## Database Schema

```
AspNetUsers          (managed by Identity — ApplicationUser)
AspNetRoles          (managed by Identity — "Admin", "User")
AspNetUserRoles      (managed by Identity)

series
  id, name, source_url, poller_type, enabled, created_at

subscriptions
  user_id → AspNetUsers.Id
  series_id → series.id

polling_config
  series_id → series.id
  interval_seconds, last_polled_at, last_change_at
```

## Notification Service — Internal Components

- **QueueConsumer** — listens on RabbitMQ
- **NotificationDispatcher** — routes to the correct channel implementation
- **`INotificationChannel` interface** — one implementation per channel
- **`MailChannel`** — sends email via SMTP (the only channel for now)
- Future channels (WhatsApp, etc.) are added as new `INotificationChannel` implementations — no existing code changed

The Notification Service has no database access. Recipient resolution happens in the C# App before the message is pushed to the queue.

## Key Architectural Decisions

| Decision | Reason |
|---|---|
| `.ics` file is the event state, not the DB | Avoids data duplication; the file is the canonical representation and serves as the previous-state baseline for change detection |
| Orchestrator is `IHostedService`, not called by REST API | Polling must run on a schedule independent of HTTP traffic |
| `IWebsitePoller` interface with concrete implementations per source | Open/Closed: new source = new class, no existing code modified |
| Pollers stay in the C# App container | Adding a poller is rare; brief restart cost is acceptable; interface is already compatible with future network-based extraction if needed |
| C# App resolves subscribers before pushing to queue | Notification Service stays stateless and simple |
| Separate Notification Service container | Independently deployable; isolates channel-specific code; easy to add channels |
| ASP.NET Core Identity for user management | Provides user/role/password management out of the box; no custom user code needed |
| JWT for authentication | Stateless; works well across REST API and static frontend |
| First registered user → Admin | Simple bootstrap; no seed scripts or hardcoded credentials |
| `IQueueAdapter` interface over RabbitMQ | Decouples Orchestrator from queue technology |
