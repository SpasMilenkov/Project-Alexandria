# Monitoring and domain reference

Service routing, deep links, incident and job lifecycles, and data quirks.

- `ServiceType` enum: `Api, MediaPreviews, DocumentPreviews, Transpilation, Lyrics,
  MediaMetadata, Playlist`. Dashboard routing lives in
  `Frontend/src/features/monitoring/service-routing.ts`: mapped services deep-link to their admin
  integration page; unmapped ones (today: `Api`) fall back to the generic filtered timeline at
  `/dashboard/admin/incident-history` — never a dead link.
- Deep-link contract (all monitoring surfaces):
  `service` (enum number), `from`/`to` (ISO strings), `severity`, `eventId`. Producers emit
  strings; consumers should parse via `parseDeepLinkQuery` (embedded feeds use
  `useServiceDeepLink`). A link without a param must clear the previous selection.
- Incident lifecycle: events are created by the health publisher / failure-rate checker /
  worker-cycle failure recorder / user reports. Health and threshold publishers resolve their
  incidents automatically; the health publisher decides per `ServiceType` (postgres/storage/
  rabbitmq all report as `Api`). `IncidentExpiryService` resolves worker-cycle failures idle for
  30 min and user reports idle for 24 h (`Monitoring:IncidentExpiry`), measured from
  `LastSeenAt`. A partial unique index allows one active incident per service and code
  (user reports excepted); insert through `AddActiveIfAbsentAsync`. Uptime is the merged union
  of Failure-severity incident intervals clipped to elapsed time (`UptimeCalculator`). Manual resolution requires admin access to
  `PATCH /api/monitoring/events/{id}/resolve`; an already-resolved read returns 409, but concurrent
  resolution is not currently protected. Notes are appended into metadata as a camelCase
  `resolutionNote` key; there is no separate note column.
- Backend `OperationalEventCode` includes `WorkerCycleFailure` (4), with metadata kind
  `worker-cycle-failure`, mirrored in the frontend enum/display/parser; keep producer and
  consumer contracts aligned when changing incident handling.
- Job recovery is database-driven (there is no RabbitMQ dead-letter exchange, and the API's
  broker user cannot read queues). `JobRecoveryWorker` (API, `Monitoring:JobRecovery`) fails
  stalled Processing jobs and retries failed previews with exponential backoff up to
  `JobRetryPolicy.MaxAutoRetries`. Lost messages need no sweep: every job message goes through
  the transactional outbox (`plans/messaging-outbox-plan.md`), so a job and its message commit
  together. Preview deliveries that keep failing before a claim are parked by `DeliveryRetry`,
  which fails the still-Queued job so the backoff retry takes it. Handlers release their claim
  (`ReleaseClaimAsync`) on shutdown or transient errors. Admins requeue any job type, including
  one stuck in Queued past its stalled threshold (broker-side message loss), through
  `POST /api/admin/jobs/{id}/requeue` and `/api/admin/jobs/requeue-failed`.
- Outbox monitoring: the `outbox` health check (reports as `Api`, excluded from readiness) fails
  once the oldest unsent row waits over `Outbox:BacklogAlertMinutes`; a message refused
  `Outbox:MaxAttempts` times goes dead and records a worker-cycle incident (`Api`, instance
  `outbox`). Dead messages are listed on the integrations hub and resent through
  `POST /api/admin/outbox/resend-dead` (audited as `OutboxMessagesResent`).
- Quirks worth remembering:
  - `TrackLyrics.ConfidenceScore` is an arbitrary **1–10 scale** — display as `x.x / 10`,
    never as a percentage.
  - `Preview` rows record successful artifacts (Thumbnail | Preview image). Separate generic
    `Job` rows persist preview outcomes; preview dashboards expose artifact volume plus job
    status, duration and failure-rate statistics. Do not infer that failures are unrecorded
    from the absence of a `Preview` row.
  - `JobOutcomeTracker` reads persisted `Jobs` through a fresh DI scope. Each worker's
    `JobFailureThresholdChecker` checks a trailing two-hour window every five minutes,
    with a minimum of ten outcomes and a failure threshold above 15%. Jobs are mutable current
    state, not an immutable history of every retry.
  - Monitoring enums are stored as strings, so never `OrderBy` a severity/status column:
    PostgreSQL sorts the names lexically. `GetCurrentStatusPerServiceAsync` uses explicit
    boolean precedence for that reason.
- Audit references: [system engineering audit](audit/system-engineering-audit.html) and
  [monitoring quality audit](audit/monitoring-quality-audit.html). Verify findings against the
  current implementation before relying on them; do not silently expand unrelated work into
  fixing the audit backlog.
