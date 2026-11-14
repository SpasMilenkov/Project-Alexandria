# Installer capability and setting inventory

Updated 2026-10-12 during P6 source-image/native delivery acceptance. Runtime evidence below
is recorded separately from configuration coverage. Trusted PC/Android browser acceptance
and the currently failing external lyrics-provider recovery check remain open.

## Shared configuration contract

Saved feature IDs remain compatible. Six toggles and one Off/Effnet/MAEST choice feed
`ResolvedCapabilities`, which snapshots the selection and drives Compose fragments,
source/template preflight, services, volumes, networks, model requirements, flags,
readiness inventory, service counts and memory estimates. Deployment rejects missing selected
services and additional unreviewed services before builds/startup.

Legacy dual audio selections retain historical Effnet behavior until the model screen explains
and confirms one choice. They never install two classifiers. Turning automatic tagging off
preserves the chosen model in metadata but omits coordinator, classifier and model preparation.

## Capabilities and actual consumers

| Choice | Services and bindings | Behavior and evidence |
|---|---|---|
| Core | Database, Garage/init, RabbitMQ/config, API, frontend, Nginx, media-worker, playlist-worker | Media metadata and playlist synchronization stay installed with all toggles off |
| Media previews | `MEDIA_PROCESSING_ENABLED` → API/media `Features__MediaPreviews` | Core audio/video jobs still collect ffprobe metadata when off; image preview dispatch is omitted; old image deliveries are cancelled. Live metadata-only → enabled-preview restart passed |
| Document previews | document-worker; `DOCUMENT_PREVIEWS_ENABLED` → API/worker `Features__DocumentPreviews` | Shared dispatch gate avoids jobs for absent consumer. Real PDF rendering passed |
| Adaptive conversion | transpilation-worker; `STREAMING_ENABLED` → API/worker `Features__AdaptiveStreaming` | New conversion/retry/backfill paths are gated. Existing representations, playlists and original-file access remain available. Real audio DASH/Opus output passed |
| Audio analysis | media-metadata-worker plus exactly one classifier; `AUDIO_ANALYSIS_ENABLED` → API/coordinator `Features__AudioAnalysis` | Upload/policy/backfill/accumulator/sweep paths share the gate. Descriptive metadata is a media-worker duty, independent of inference |
| Model choice | `ESSENTIA_BACKBONE` → coordinator `Essentia__Backbone`; classifier `BACKBONE` and `QUEUE_NAME` | Effnet uses classify-effnet-queue; MAEST uses classify-maest-queue. Both real coordinator/broker/inference/result/database/cleanup chains passed; omitted model has no consumer |
| Automatic tagging | `AUTOTAGGING_ENABLED` → API/coordinator `Features__Autotagging`, frontend tagging controls | Master policy plus selected model determines effective analysis; master off installs no idle classifier |
| Lyrics | lyrics-worker; `LYRICS_ENABLED` → API/worker `Features__Lyrics` | Identity lyrics_worker_user, lyrics-queue/lyrics.# and parking queue; optional producers/refetch paths gated. Requires outbound HTTPS to lrclib.net. Real outage produced a counted retry, then provider recovery persisted NoMatch and acknowledged delivery |
| Playlists | Core playlist-worker; playlist_worker_user; content-exchange, playlist-queue, playlist.sync | Actual JSON notifications and periodic sweep passed. DB/settings graph does not currently resolve S3; ordinary read-only worker S3 credentials retained for common registration |
| Monitoring | Prometheus, Grafana, cAdvisor, postgres-exporter, Alloy, Loki | Fresh isolated checks passed three dashboards, 21 queries, eight scrape targets, log isolation, token readability and HTTPS proxy/assets/websocket. Maintainer confirms preview metrics; preview log ingestion subsequently fails with Loki HTTP 500 despite readiness passing. Deferred; see P6 evidence below |

Optional durable work already queued before disabling a feature is retained for re-enable;
it is not silently purged. Core media handles old image deliveries explicitly when previews
are off. Consumers, retries and backfills respect the same backend processing options. An
explicit unavailable API response replaces new work for disabled optional consumers.

The frontend's legacy `VITE_STREAMING_ENABLED` flag controls the entire player/navigation,
not adaptive conversion alone. Installer builds set it true independently of `STREAMING_ENABLED`,
so disabling new conversions does not remove existing playback or playlists. No new raw-audio
fallback in the adaptive music player was added; its existing ready-representation contract remains.

## Settings ownership and bindings

| Concern | Owner and actual consumer |
|---|---|
| Source/setup folders | Installer; absolute build contexts/static source mounts, target-relative generated artifacts, command working directories |
| Project/volume identity | Generated or verified legacy identity; Compose commands and volume ownership checks. Persisted COMPOSE_PROJECT_NAME agrees with ProjectName |
| LAN origin and TLS | Installer; origin-derived BASE_URL, S3 public endpoint, CORS, Grafana root URL; generated proxy and certificate validation |
| Admin account | Installer ADMIN__* → API bootstrap/Identity; established account credentials preserved on repair |
| Database identity | Generated/preserved .env; Postgres bootstrap and DB_CONNECTION_STRING → AlexandriaPostgres in API/workers |
| Broker identity/vhost | Rendered definitions; each worker RabbitMQ user/password/vhost and actual consumer queue/exchange |
| Garage RPC/admin/metrics | Protected Garage TOML; init/API provider; Prometheus mounted token file |
| S3 keys | Garage init handoff → verified protected .env → deleted handoff; generation-only persistence receipt prevents normal re-export; separate application, preview and transpilation identities |
| JWT/CSRF | Protected generated/preserved values; API authentication options |
| Frontend build | Explicit nonsecret Vite arguments: /api, PBKDF2 800000, player enabled, chosen tagging policy |
| API worker health | Six explicit names/URLs; installed-set enabled flags for document, media, transpilation, lyrics, coordinator and playlist. Essential /health/ready excludes optional workers; enabled failures remain in full health/status |
| Grafana | Preserved admin credentials and HTTPS /monitoring/ root; anonymous signup disabled; bundled dashboard JSON and datasource provisioning |
| Exporter | Preserved POSTGRES_EXPORTER_PASSWORD; Postgres init user and monitoring exporter DATA_SOURCE_* |
| Models | Installation cache; image-owned checksum/requirements; offline source input or explicit wizard opt-in for pinned HTTPS downloads |
| Storage layout | StorageSettings → GARAGE_CAPACITY and explicit change-request flag → Garage init; actual capacity receipt persisted after infrastructure readiness |
| Docker resources | Daemon CPU/RAM and local Linux DockerRootDir filesystem; Docker Desktop VM disk remains unknown with explanation |
| Appearance/behavior/upload policy/quotas | Application database/settings APIs. Installer adds no invented environment overrides |
| Backup/restore | Documented consistent whole-project settings/volume workflow; no automatic scheduler or schema downgrade |

Generated Compose explicitly supplies internal S3 endpoint/provider/region/bucket names,
broker host/port, essential API checks, console sinks and the transpilation polling interval.
Tracked `appsettings.Template.json` files are examples, not automatically loaded defaults;
installer acceptance mounts empty app settings to catch dependencies on ignored local files.

Application settings keep their existing defaults and database ownership. `/settings` controls
appearance (amber accent, parchment background, thumbnails/transparency/blur enabled) and
behavior (delete confirmation, all toast levels, automatic tag regression and metadata overwrite
off, automatic-playlist creation threshold 10). Administrators change upload policy at
`/dashboard/admin/system-configuration`; trusted-upload validation skipping and hash-match
upload skipping both default off. New accounts receive the existing 10 GiB storage quota;
administrators manage individual quotas in the user registry. This quota is independent of
the installer's decimal-GB Garage layout capacity.

## Models, storage and resource accounting

- Selected models are copied to temporary files, size/SHA-256 verified, then atomically promoted.
  Verified cache survives retry/cancellation; corrupt/partial files never count as ready.
  Default model preparation is offline. The wizard can explicitly permit missing/corrupt
  selected assets to download from `essentia.upf.edu`; this reviewed choice persists for repair.
  Verified local files are reused and analysis Off skips preparation. Permission to pull
  Compose images does not enable model downloads.
- Effnet requires 17 files, 24,056,930 bytes. MAEST requires 17 files, 370,003,909 bytes:
  its genre model plus shared Effnet embeddings and seven mood heads/metadata pairs.
  Runtime verifies its selected subset against image-owned pinned definitions, regardless of a
  mutable mounted checksum file. Full broker-chain inference passed under UID/GID 1001 and a
  2 GiB cap: Effnet peaked at 662,327,296 bytes (632 MiB), MAEST at 1,369,579,520 bytes
  (1.276 GiB). These are fixture peaks, not universal maxima.
- New Garage layout defaults to 10 decimal GB, minimum 1 GB verified against Garage v2.2.0.
  Repairs read/preserve actual capacity. Existing layout changes are a separate operation and
  conflicting reviewed requests fail without resizing or deleting data. Capacity is neither
  filesystem preallocation nor per-user quota. Unknown legacy capacity stays unknown until read.
  Live acceptance stored an object at 1 GB, preserved it and the actual-capacity receipt on
  a stale-10-GB rerun, then rejected a reviewed 2 GB change while retaining both.
- Docker filesystem free space is shared by volumes, images and temporary processing. Models
  and setup files use the installation filesystem; leave additional space for these and backups.
  Stored data usage and per-user quotas remain application-owned; free space is not a quota.
- Core estimates total 4096 MB (eight ongoing services plus two zero-steady-memory setup jobs).
  Coordinator adds 1024 MB, selected classifier 2048 MB. Monitoring adds 1536 MB including
  headroom. Document/transpilation add 1024 MB each; lyrics adds a 512 MB planning allowance.
  Totals/charts are planning estimates, not measured minimum requirements or summed reservations.
- Inference prefetch/concurrency is one job with a limit of eight CPU cores and 2 GB memory.
  Daemon/VM resource limits inform warnings. Heavy jobs can still cause sustained CPU load.

## Deliberate differences from reference Compose

- Exactly one selected HTTPS publication through Nginx; Grafana shares /monitoring/.
  Internal application, storage, broker, exporter, metrics and log ports are not published.
- Project-scoped names and volumes for new installations; verified legacy identities retained.
  Source/build assets can remain separate from generated settings and model cache.
- One effective classifier, selected coordinator routing and master-policy gating replace
  reference dual-classifier inclusion. Core media and playlist services remain installed.
- All third-party template images use the exact reference versions. Existing reference hashes
  are retained; Grafana/infrastructure tags additionally use cached immutable digests.
- Prometheus scrapes only actual metrics endpoints: API, Garage, RabbitMQ, exporter, cAdvisor,
  itself, Alloy and Loki. Workers expose health, not fictional /metrics endpoints. Prometheus
  is outside the classifier network. cAdvisor retained metrics and Alloy logs are project scoped.
  API-only legacy metric-name negotiation keeps pinned Prometheus 3/OpenTelemetry series
  consistent with dashboard queries; the live test caught dotted names before this fix.
- The protected 0600 metrics token is read by root Prometheus with only DAC_OVERRIDE retained;
  remaining capabilities are dropped. Grafana remains under its image user.
- cAdvisor retains the reference privileged container and read-only host mounts, including
  `/var/lib/docker`. Alloy reads the Docker socket to discover/tail containers; a read-only socket
  bind does not restrict Docker API authority. Project filters scope retained metrics/logs,
  not host access. These host-level permissions are monitoring requirements, not WAN exposure.
- Loki's pinned image contains only its binary, so an invented shell healthcheck is omitted.
  Final monitoring readiness probes its real /ready endpoint internally using Grafana's wget;
  Prometheus's bundled wget cannot resolve Docker names in this pinned image.

## Validation boundary

Configuration coverage checks all 192 supported combinations (six toggles × three models),
with explicit legacy-dual tests. It compares independently specified expected service/RAM
sets and generated volumes/networks/flags. The final opt-in real Compose matrix passed for
all 192 selections. Ordinary tests cover configuration persistence, source
preflight, capability gates, model integrity/cancellation and storage/resource behavior.

Real worker acceptance has passed metadata-only processing, restart with previews, PDF
rendering, stored thumbnails/previews, DASH/Opus conversion, playlist notifications and sweep.
Both actual inference models passed isolated cached runtime and complete broker-chain checks.
Lyrics outage/recovery, full monitoring dashboard queries and authenticated TLS proxy checks
also passed with empty application settings. Builder: 695 passed, 13 opt-in checks skipped in
the ordinary run; all 12 Docker checks passed in focused opt-in runs. Backend: zero-error build,
832 unit tests passed. Python model/layout suites: 13 and 6 passed. Frontend production build
passed with its unchanged TypeScript baseline; no TypeScript source was edited.

The P4 fixtures used unique disposable projects, current compiled outputs over cached images,
and scoped cleanup. P6 packaged-image and native executable evidence is recorded below.

## Deployment lifecycle acceptance (P5)

The final Builder build passed with zero warnings/errors and all 770 ordinary tests passed.
Production process execution is injected, concurrent and bounded; silent/noisy real terminal
checks passed theme switching, resize, cancellation 130, terminal restoration and child-tree
cleanup. Exact Compose service/replica/state/health/init checks run before success. No-healthcheck
services are qualified by process state. Actual runtime status parsing used installed Compose 5.6.0.

Fresh administrator HTTPS login, Admin role/identity, secure cookies and authenticated profile
passed; wrong credentials were rejected without changing the account. Repair preserves existing
accounts and reports its credential-verification limit. The current API does not define an active
CSRF header/challenge exchange. Final completion follows all required service/public/auth checks.

Persistent owner-only logs redact secrets before UI/disk and exclude raw resolved Compose and
process environment. Recovery offers retry/resume, status/service logs, scrollable evidence,
summary return and exit while preserving services/data. Retry checks artifacts/environment/
ownership and validates Compose again. Final `INSTALLATION.txt` records trust/access/data/log
locations and correctly quoted project-specific operational commands without credentials.

Real restart and stopped-worker repair passed with encrypted uploaded/downloaded data, metadata,
credentials, certificates, sessions and volume attachments retained. A separate disposable
installation stayed unchanged. Canonical environment rewrites and Garage header timestamps are
idempotent. These P5 checks establish runtime repair using cached images and current output.
Reproduction steps are in the integration test README.

## Source-image and native delivery acceptance (P6, in progress)

- All 11 repository source images built from a clean copy without ignored settings,
  certificates, models, host application outputs or node_modules. All packaged image
  entrypoint/native-tool probes passed. Backend contexts include the complete transitive
  project graph; frontend pnpm matches its declared version; source checkout ordering and
  Docker context filters were corrected.
- Source-mode Docker fixtures pin actual immutable image IDs and retain image-owned bundles,
  entrypoints and settings. They reject host application overlays and execution overrides.
  Five checks passed: complete HTTPS/auth/encryption/monitoring; real previews, PDF, streaming
  and playlists; both coordinator/broker/classifier/result/database chains; and Garage
  bootstrap, normal repeat and explicit same-key recovery.
- The sixth live check reached the real lyrics consumer and validated outage handling, but
  recovery failed because lrclib.net returned HTTP 503 from the Docker network. This remains
  failed/unverified; the host's separate provider probe returned 404. No provider/network
  workaround or additional dependency was installed.
- Native AOT published offline with existing Ubuntu 24.04 x64 .NET 10.0.12 packs and GCC.
  The actual executable ran outside the checkout with spaced source/target paths, matched all
  14 shipped templates, and completed fresh core, maximal MAEST and maximal Effnet/monitoring
  setups. Validation host: Pop!_OS 24.04 with Ubuntu 24.04 runtime packs; Linux amd64 Docker
  Engine 29.9.0 and Compose 5.6.0. Other platforms are not advertised.
- Native feature removal retained all 14 original named volumes while removing disabled
  containers. A separate recommended installation resumed after temporary Docker address-pool
  exhaustion and completed with independent project, trust and volume identities. All 21
  existing preview container identities/start times/data attachments remained unchanged.
- Fixed a live credential lifecycle gap: later Compose startup previously recreated the
  consumed Garage handoff. Keys are verified in .env before a protected generation-only receipt
  is published and the handoff deleted. Native setup and ordinary restart kept it absent,
  preserving all six S3 keys and certificates. Failed persistence retains the recovery copy.
- Final Builder build: zero warnings/errors; ordinary suite: **849 passed**. Focused credential,
  orchestrator and source-manifest tests: **73 passed**; offline Garage script tests: **14 passed**.
  Native and source-image checks use existing local model assets and authorized Dockerfile
  dependencies only. No host tools, packages or models were installed.
- Final backend build: zero warnings/errors; **832 unit tests passed**. Real silent/noisy
  terminal checks passed. Frontend production source image built successfully; type checking
  still reports the same **10 existing errors**, with no TypeScript source edits. Documentation
  production build passed all **9 pages** using existing dependencies.
- A native preview archive packages the verified executable, all 14 templates and checksums.
  It contains no generated credentials, private keys or models. Matching clean source and
  verification evidence are separate artifacts; no public distribution is claimed.

The maximal Effnet preview remains on `https://192.168.0.106:10443` with one published HTTPS
port. The maintainer confirmed streaming and file upload/download from both PC and mobile,
plus working Effnet automatic tagging. Effnet is the deliberate preview selection; MAEST
was verified separately by the automated inference chain. Further browser checks confirmed
video, audio, DOCX, images and encrypted uploads/downloads; the maintainer's song also fetched
lyrics successfully. Grafana login, three provisioned dashboards, populated API/container
metrics and API/database/broker/storage scrape indicators are confirmed. The preview Logs
dashboard is empty: Alloy batches receive HTTP 500 from Loki, `Ingester is shutting down`,
although Loki readiness and Grafana datasource health pass. Current readiness checks do not
prove log ingestion. Earlier isolated log-ingestion successes are not current preview evidence.
Logs Drilldown is an absent additional plugin; automatic plugin installation is disabled.
The maintainer directed deferral of nontrivial monitoring configuration. No plugin was installed
and no runtime configuration was changed during diagnosis.

The maintainer accepts manual client trust of the automatically generated certificates;
warning-free client trust was not independently verified. The earlier automated lyrics
outage/recovery failure remains recorded until a rerun passes. Permanent settings/source
placement is also outstanding:
both preview folders are under `/tmp`, and ordinary repair is not a relocation facility.
The preview uses a 10 decimal GB Garage layout on Docker-managed volumes. See the separate
release notes for the proposed permanent layout and handoff requirements. P6 remains open
until required remaining checks are resolved or explicitly accepted.
