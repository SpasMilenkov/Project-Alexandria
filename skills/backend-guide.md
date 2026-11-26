# Backend guide

Backend conventions for Project Alexandria. `AGENTS.md` keeps the short list of rules that
bite most often; this file holds the full detail.

## Project map

Solution: `Backend/AlexandriaBackend.slnx` (21 projects). Project references are the authority
for dependency direction. Workers also host HTTP health endpoints.

| Path | What it is |
|---|---|
| `Backend/Alexandria.Common` | Interfaces, policies, config, shared repositories contracts |
| `Backend/Alexandria.Data.Models` | EF entities + enums + `ValidationConstants` |
| `Backend/Alexandria.Data` | DbContext + entity configurations + migrations |
| `Backend/Alexandria.Dto` | Request/response records, extension helpers |
| `Backend/Alexandria.Repositories` | EF Core repository implementations |
| `Backend/Alexandria.Infrastructure` | DI wiring, auth/JWT, storage, messaging |
| `Backend/Alexandria.Api` | FastEndpoints host — features live in `Features/<Area>/<Feature>/` |
| `Backend/Alexandria.Services.Monitoring` | Monitoring/analytics services shared by Api + workers |
| `Backend/Alexandria.Workers.*` | Six worker hosts: Document, Lyrics, Media, MediaMetadata, MediaTranspilation, Playlist |
| `Backend/Alexandria.Tests.Unit` | Unit tests (xUnit v3 + NSubstitute + AwesomeAssertions) |
| `Backend/Alexandria.Tests.Integration` | Full-stack tests over real containers |
| `Backend/Alexandria.Tests.Common` | Fixtures, builders, fakes shared by both test projects |
| `Builder/` | Local-setup TUI installer (Spectre.Console, net10, AOT). Generates `.env`/`docker-compose.yml`/`garage.toml` from `Templates/Local/*`; two-phase boot merges garage-init keys. Plans: archived `completed/builder-*plan.md` and active `plans/builder-completion-plan.md` |

## JSON serialization (the #1 bug source here)

- **Enum-typed DTO fields serialize as numbers.** No `JsonStringEnumConverter` is configured
  and none should be added. Frontend mirrors backend enums as numeric TS enums with matching
  ordinals (see `Frontend/src/enums/`). Explicit string-valued contracts, such as health status
  and enrichment status names, remain strings; check the DTO rather than assuming every
  status property is an enum on the wire.
- Dictionary keys serialize as *strings* even when values are numbers — normalize on read.
- **Never call the one-argument `JsonSerializer.Serialize(x)`.** Pass explicit options for
  the intended property naming. For polymorphic DTOs, also serialize through the abstract
  base type so its discriminator is emitted; options alone do not fix serialization through
  a derived type. Reference: `Alexandria.Dto/Extensions/OperationalEventExtensions.cs`.
- Other raw-serialization call sites pass explicit `JsonOptions` (see MediaMetadata workers).

## Dates & Npgsql

- `timestamp with time zone` columns reject `DateTimeKind.Local`/`Unspecified`. Query-string
  dates bound by FastEndpoints arrive as `Kind=Local` — so request DTOs take
  **`DateTimeOffset`/`DateTimeOffset?`** and handlers convert with `.UtcDateTime`.
- `DateOnly` serializes as `"yyyy-MM-dd"`. Daily/hourly analytics bucket on UTC days; keep
  frontend day-window construction aligned (`Date.UTC(...)`, see
  `Frontend/src/features/monitoring/service-routing.ts`).
- Analytics at self-host scale: **fetch once, bucket in memory** (plain LINQ group-by). This
  keeps all math in unit-testable C# — do not reach for raw SQL date functions unless an
  aggregate genuinely cannot be done in memory. Use `FromSqlInterpolated` (never `FromSqlRaw`)
  if SQL is unavoidable.

## Endpoints (FastEndpoints)

- Feature folder per operation: `Features/<Area>/<Feature>/<Name>Endpoint.cs` containing the
  request DTO, its `Validator<TRequest>` (auto-discovered — no manual registration), and the
  endpoint. Routes carry no version segment; the `api` prefix comes from config.
- Auth: `Policies(Common.Auth.Policies.RequireAdmin | RequireUser)`; anonymous endpoints use
  `AllowAnonymous()` explicitly (everything else defaults to RequireUser via configurator).
- Responses: `Send.OkAsync(...)` / `Send.NotFoundAsync(ct)` etc. **Business-rule conflicts use
  the validation channel**: `AddError(x => x.Prop, "message")` +
  `await Send.ErrorsAsync(statusCode, ct)` — precedent `AddRuleEndpoint.cs`. Do not invent
  custom result shapes for conflicts.
- Stats/analytics endpoints: inject the stats service and mirror
  `Features/Admin/TranspilationStats/` for endpoint structure. Authenticated monitoring/stats
  endpoints send `ResponseCache(0, ResponseCacheLocation.None, noStore: true)`: cookie auth adds
  an Authorization header, so the server response cache never applies to them, and a
  `public, max-age` header only made the browser serve stale data after Colada invalidations.
  Anonymous endpoints (`/status*`) may use `ResponseCache(seconds, varyByQueryKeys: [...])`;
  always list every query parameter, otherwise one caller's parameters poison everyone's cache.
- New service implementations register in
  `Backend/Alexandria.Infrastructure/ServiceExtensions.cs`.

## Testing (backend)

- xUnit v3 + NSubstitute + AwesomeAssertions. Pattern: `CreateSut()` factory methods, mocks via
  `Substitute.For<T>()`, assertions fluent. Always pass
  `TestContext.Current.CancellationToken`.
- Test names: `method_condition_expectation`.
- Time-sensitive logic: anchor timestamps to **fixed UTC hours** around `DateOnly.FromDateTime(
  DateTime.UtcNow)` — never relative offsets like `-2.5 days`, which drift across window edges
  depending on run time.
- To unit-test internals: make the member `internal` and add `<InternalsVisibleTo
  Include="Alexandria.Tests.Unit"/>` in the project file (established precedent).
- Integration tests use `Tests.Common`: `AlexandriaFixture` spins Postgres/RabbitMQ/Garage via
  Testcontainers and Respawns between tests; `FullStackTestBase` gives per-test
  `WebApplicationFactory` clients (`Auth` = plain user, `Factory.CreateAuthenticatedClient(id,
  "Admin")` for admin). Builders live in `Tests.Common/Builders`.

## Messaging (backend)

- Publish only through `IOutbox.EnqueueAsync`, inside the unit-of-work transaction that writes
  the change the message announces (`unitOfWork.InTransactionAsync(...)` or
  `BeginTransactionAsync`/`CommitAsync`). Repositories save on `AddAsync` and `ExecuteUpdate`,
  so the transaction is what keeps the job row and its message together; enqueueing outside one
  throws. A nested transaction joins the outer one.
- A message that starts a job goes through `IOutbox.EnqueueJobAsync(routingKey, jobId, ct)`. The
  body is the job id, and the row keeps it as `JobId`, so a message that dies fails its job while
  still Queued instead of leaving it waiting for a delivery that never comes.
- `IPublisherService` is for the outbox relay and `DeliveryRetry` only. It confirms every
  publish and uses `mandatory: true`, so a refused or unroutable message throws.
- Guard user-facing actions with `MessageBrokerUnavailableException.ThrowIfDisabled` (server
  without a broker). A broker that is only down is not an error: the outbox holds the message.
- Consumers handle failed deliveries through `DeliveryRetry` (counted republish, then a
  worker-specific park callback), and must tolerate duplicates: delivery is at least once.

## Methods & repositories (backend)

- `CancellationToken ct` is always the last parameter. New optional parameters go
  before it; update existing callsites instead of appending after it.
- Repository classes hold only methods. Projection rows, helper records, and other
  supporting types live in their own file, never nested inside the repository class.
