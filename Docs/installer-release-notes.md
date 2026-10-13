# Installer progress notes

Personal milestone recap for the Alexandria Builder installer.

## 2026-10-12 — Model download automation validated

- Audio models offers local files by default or explicit HTTPS downloads of missing/corrupt
  selected assets. Review shows the choice; it persists for repair and can be switched off.
- Downloads use pinned `essentia.upf.edu` assets with byte-size/SHA-256 checks, atomic replacement,
  cancellation and bounded retries. Verified local files are reused; Off performs no preparation.
- Corrupt source/cache recovery works with consent, including the installation-owned cache.
  No Python or new host dependencies are required by Builder's downloader.
- Verification: **876 Builder tests passed**, build 0 warnings/errors, native publication passed.
  Native wizard exercised the choice and review, then cancelled without deployment or settings
  changes. Download behavior was tested with synthetic data/fake HTTP; no real models downloaded.
- Permanent native installer and source are updated at `~/alexandria/`.
  Run `/home/spasmilenkov/alexandria/run-installer` to review or repair this installation.
  Existing runtime models/settings remain offline; no second deployment for this feature.


## 2026-10-12 — Permanent settings migration validated

- Source, settings and native installer moved to `/home/spasmilenkov/alexandria/`.
  `run-installer` selects the established settings/source from any working directory.
- All 21 containers use permanent paths. Same 14 data volumes, image IDs, generation,
  credentials, certificates, cached models and existing file/account records were retained.
- Builder inspection/repair ownership, service readiness, HTTPS and native launcher passed.
  The maintainer reports the migrated installation works. Current passwords were preserved.
- Original settings and a private database dump remain in protected `migration/` for rollback.
  Active settings no longer depend on `/tmp`; historical logs retain their original paths.
- Verification: 853 Builder tests passed, 6 relocation tests passed, build/native publication
  succeeded without new host dependencies, image builds/pulls or model downloads.
- Detailed paths and operations: [permanent-installation.md](permanent-installation.md).
  Model download automation is the next authorized step; monitoring configuration is deferred.


## 2026-10-12 — UI redesign reference delivered

- Root-level `tui-redesign-reference/` groups a linked file map, 86 unchanged source copies
  and 68 numbered PNG/SVG captures covering all 12 wizard screens, subpanels, principal
  validation/recovery states, both themes and narrow terminal layouts.
- `index.html` is a searchable local gallery; no server is needed. Original ANSI/plain-text
  frames, source fingerprints and an offline reproduction tool are included.
- Captures use the actual screen renderables with sample data and disabled external actions.
  No second deployment, Docker command, network request, model download or host installation
  was performed. The running preview and production UI code were untouched.

## 2026-10-12 — Browser acceptance and permanent-installation handoff

- Maintainer priority: permanent settings/source placement. Automatic certificate generation
  is established; manual client trust is accepted. Nontrivial monitoring work can wait.
- Model sourcing already has an opt-in script: `classificators/essentia/download-models.sh`.
  Its `--download` option fetches missing selected-backbone assets, verifies pinned sizes and
  SHA-256 hashes, and reuses verified cache files. Ordinary Builder preparation remains offline;
  a wizard download choice is not implemented. No models were downloaded during this review.
- PC/mobile file transfer and streaming, encrypted files, video/audio/DOCX/image previews,
  Effnet tagging and actual song lyrics are confirmed by the maintainer.
- Grafana login and all three provisioned dashboards are confirmed. API/container metrics
  populate and the API/database/broker/storage scrape indicators are up.
- The preview's Logs dashboard is empty. Alloy reaches Loki, but Loki rejects log batches
  with HTTP 500, `Ingester is shutting down`, despite passing readiness and datasource health.
  Fresh isolated log-ingestion acceptance passed earlier; that does not establish the current
  preview's logging works. The installer readiness check does not verify ingestion.
- Grafana Logs Drilldown is not installed; automatic plugin installation is disabled.
  The maintainer accepted deferring nontrivial monitoring work. No plugins were installed and
  no runtime configuration was changed during this diagnosis. Metrics remain usable.
- At this earlier checkpoint permanent placement was outstanding: preview settings and source
  mounts were under `/tmp`. The subsequently verified layout is `~/alexandria/source`,
  `~/alexandria/settings` and `~/alexandria/installer`. Moving the preview requires a planned
  migration preserving project/volume identity, credentials and certificates; copying folders
  alone conflicts with saved paths and Docker ownership labels.
- Current settings: `192.168.0.106:10443`, Effnet with all six optional toggles enabled,
  and **10 decimal GB Garage layout capacity**. Capacity is not a quota or allocated disk;
  data remains on Docker's data filesystem. Select the intended capacity before a fresh
  installation. Existing layout resizing is a separate operation, not ordinary repair.

**Before handing the installer to someone else:** supply the matching full source checkout
and executable/templates bundle (or an existing .NET 10 SDK for source execution), a supported
Ubuntu 24.04 x64/local Docker host, and verified local audio models or select Off. Initial
Docker builds may fetch their declared dependencies. Client certificate trust, consistent
settings-and-volume backups, certificate renewal and upgrades remain manual. There is no
automatic backup scheduler, schema rollback, remote-daemon support or tested cross-platform
release. The current preview's log ingestion is a recorded defect, not a passed check.

## 2026-10-12 — P6 delivery checkpoint (in progress)

- Native AOT publication succeeded offline with the existing Ubuntu 24.04 x64 .NET
  10.0.12 packs and GCC. All 14 published templates matched source.
- The real native executable ran in a separate terminal working directory, accepted clean
  source and target paths with spaces, reached the reviewed summary and cancelled with exit
  130. The terminal was restored and no installation artifacts/services were created.
- Platform preflight now checks a glibc Linux x64 host and local Linux amd64 Docker daemon,
  including another check before deployment writes. Focused validation: **70 tests passed**;
  Builder build: **0 warnings/errors**.
- Clean Docker build contexts exclude local settings, certificates, build outputs and models.
  Fixed missing transitive project copies, frontend package-manager parity and pinned source
  checkout ordering. All **11 source images built successfully**, including the native media,
  document and inference tools; all 11 packaged entrypoint/tool probes passed.
- The published native installer completed a fresh core installation and a maximal MAEST
  repair with monitoring from clean source, using spaced paths and the selected LAN HTTPS
  port. Existing local models were copied and verified; no models were downloaded.
- Disabling all optional features through the native installer removed their containers while
  preserving all **14 existing named volumes**, project identity and certificate trust material.
- Ordinary Builder validation reached **849 passing tests**. Source-image runtime fixtures now
  verify immutable built image IDs and reject host application overlays.
- A live check caught `garage-init` recreating its temporary credential handoff after the first
  successful merge. Fixed this with a generation-bound non-secret receipt. The native installer
  and ordinary Compose restart both kept the handoff deleted, preserving all six Garage keys
  and certificates. **73 focused .NET tests**, **14 offline Python tests** and shell syntax
  validation passed; explicit forced recovery re-exports established keys without rotating them.
- Five packaged-image runtime checks passed: HTTPS/auth/encryption/Grafana, previews/PDF/
  streaming/playlists, Effnet, MAEST and Garage repeat/recovery. The sixth (lyrics recovery)
  failed because the real provider returned HTTP 503 from Docker; outage retries worked.
- A separate recommended native installation resumed after temporary Docker network address-pool
  exhaustion. Its project, trust and volumes were independent; all 21 existing preview container
  identities/start times/volume attachments stayed unchanged. Only the second test was cleaned up.
- Installation, first steps, operations, recovery, consistent backup/restore and native build
  docs were rewritten. Docs production build passed: **9 pages**, existing dependencies only.
- Historical Builder plans now point to the current requirements and validated release notes.
- Final backend build: **0 warnings/errors**; **832 unit tests passed**. Silent/noisy PTY
  checks passed. Frontend source-image production build passed; the same **10 existing
  TypeScript errors** remain, with no TypeScript source edits.
- Native preview archive is packaged with all 14 matching templates and checksums, without
  credentials, private keys or models. The maximal Effnet/monitoring preview is left running
  at **https://192.168.0.106:10443** for the maintainer.
- Maintainer browser acceptance: streaming and file upload/download worked on both PC and
  mobile. Effnet automatic tagging also worked. This preview deliberately selects Effnet;
  MAEST remains separately verified by the automated inference chain, not this browser test.
- Further browser acceptance: video, audio, DOCX and images worked; encrypted uploads and
  downloads worked. Lyrics fetched successfully for the maintainer's song, confirming a live
  provider success after the earlier Docker HTTP 503. The automated outage/recovery test has
  not yet been rerun. Subsequent Grafana feedback and the log-ingestion defect are recorded
  in the handoff checkpoint above.

**Stopped after validation as requested. Still required:** a rerun of the previously failing
automated lyrics recovery check. Manual client trust is accepted by the maintainer; automatic
certificate generation is verified. Grafana metrics are
confirmed; the preview log-ingestion failure is deferred at the maintainer's direction.
P6 remains open. Only existing
Dockerfile dependencies were authorized for downloads inside disposable builds; no host tools,
packages or model assets were installed.

## 2026-10-12 — P5 validated; stop before P6

**Deployment lifecycle milestone complete.**

- Builds stay responsive to theme changes, resize and cancellation even with no output.
  Cancellation drains output, stops child processes and restores the terminal.
- One injected process runner handles Docker/system checks with concurrent pipes, deadlines
  and cancellation. Exact readiness checks identify missing, stopped, unhealthy or duplicate
  services; services without healthchecks are described honestly.
- Fresh administrator login is verified through real HTTPS authentication, secure cookies,
  Admin identity and authenticated profile. Repair keeps existing passwords and states that
  existing-account sign-in has not been verified.
- Protected timestamped logs retain redacted commands, stages, exits and evidence. Failure
  recovery offers retry/resume, status, service logs, scrollable evidence, summary return and
  exit preserving services/data. Retry revalidates settings and generated artifacts.
- Completion is saved after required checks. The final screen and `INSTALLATION.txt` show
  access/trust/account/capabilities, settings/data/log locations and start/stop/repair commands.
- Fixed environment blank-line growth and changing Garage header timestamps on repair.
- Live restart and stopped-worker repair retained encrypted files, metadata, credentials,
  certificates, sessions and named-volume attachments. A separate installation stayed unchanged.

**Verified:** Builder build **0 warnings/errors**; **770 ordinary tests passed**; both live
HTTPS administrator and repair checks passed in focused runs; silent/noisy Linux PTY checks
passed theme/resize/Ctrl+C **130**, terminal restoration and child-tree cleanup. Disposable
containers, volumes and networks were removed. No tools, packages, models or images were
installed/downloaded/built during P5; automated HTTPS used only ephemeral loopback ports.

**Remaining:** P6 clean source-image builds, native/AOT packaging and complete delivery matrix;
permanent client CA trust and expanded manual browser checks. **Stopped here as requested.**

## 2026-10-12 — P4 validated; stop before P5

**Worker, model, storage and monitoring milestone complete.** Full installer lifecycle and
delivery acceptance remain for P5/P6.

**Achieved**

- One Off/Effnet/MAEST choice replaces independent model checkboxes. Legacy dual selections
  keep historical Effnet behavior until reviewed; tagging master off omits inference/models.
- Core media metadata and playlist synchronization remain installed. Optional job producers,
  retries and backfills respect installed capabilities; adaptive conversion off preserves
  existing playback and playlists.
- Real processing passed: metadata without ML/previews, media thumbnail/preview, PDF rendering,
  DASH/Opus conversion, playlist notifications/sweep, and lyrics lookup with outage retry/recovery.
- Both complete inference chains passed: coordinator → broker → classifier → result → database
  → shared-file cleanup. Each uses 17 verified assets; Effnet needs 24 MB, MAEST 370 MB.
  Measured classifier peaks were 632 MiB and 1.276 GiB under the existing 2 GiB cap.
- Model preparation copies existing local assets, verifies size/SHA-256 before promotion,
  reports progress, supports cancellation and retains verified cache across retries.
- Garage sizing is configurable (10 decimal GB default, 1 GB minimum). A real stored object
  survived a stale-default rerun and rejected resize. Resource displays use Docker daemon/data
  filesystem measurements and explicitly show unknown measurements.
- Three Grafana dashboards, eight healthy scrape targets and project-scoped logs/metrics work
  through authenticated HTTPS. Fixed Prometheus metric naming, protected-token access and
  actual readiness probes. All 21 dashboard queries succeeded.
- API/all six workers use a dependency-free .NET health probe. Explicit Compose bindings fix
  missing fresh-install S3/broker settings, core readiness, console logging and the transpilation
  timer. Acceptance uses empty app settings so local files cannot mask missing configuration.

**Verified**

| Gate | Result |
|---|---|
| Builder build | 0 warnings, 0 errors |
| Builder ordinary suite | 695 passed; 13 opt-in skipped |
| Docker acceptance | All 12 checks passed in focused runs, including all 192 supported Compose selections |
| Backend build / full units | 0 errors / 832 passed; focused lyrics/health tests 90 passed |
| Python model / layout units | 13 / 6 passed |
| Frontend | Production build passed; unchanged TypeScript baseline; no TypeScript source edits |
| Cleanup | Disposable projects/listeners removed; existing user services preserved |

Only the five explicitly authorized monitoring image pulls were performed; versions/digests
match reference Compose. No other tools, packages or model assets were installed/downloaded,
and no images were built. No router, firewall or trust-store changes were made; automated
HTTPS acceptance used one temporary loopback port.

**Next:** P5 cancellation/responsiveness, persistent redacted logs, readiness/recovery and repair
UX; then P6 source-image/native-AOT delivery and complete installation acceptance. Permanent
client CA trust and expanded manual browser checks remain open. Details and reproducible
test instructions are in [the inventory](installer-capability-inventory.md) and
[the test README](../Builder/Builder.Tests/Integration/README.md).

## 2026-10-11 — P4.1 shared capability foundation

**Validated and stopped before worker wiring.** Full P4 remains open.

**Achieved**

- One service/capability catalog now drives generated Compose fragments, volumes, networks,
  source/template preflight, readiness expectations, feature flags, TUI service lists and RAM
  charts. Shared dependencies and model requirements are included once.
- Deployment checks the actual Compose inventory against the reviewed selection before
  building or starting services, rejecting missing selected or additional unreviewed services.
- TUI counts distinguish ongoing services from setup jobs. RAM totals and charts share the
  same estimates, show headroom, and include a provisional coordinator allowance instead of zero.
- Selected audio models are listed as requirements; analysis-off has no classifier/model
  prerequisite. No model preparation or download was executed.
- Preflight now checks the mounted Postgres exporter initialization script. The catalog also
  records the messaging setup helper's existing default network, caught by the real parser.

**Verified**

- Final Builder build: **0 warnings, 0 errors**. Ordinary suite: **702 passed, 0 failed,
  5 opt-in checks skipped** in 57s; includes the 256-selection matrix and rendered TUI checks.
- Opt-in Docker Compose matrix: **1 passed** in 38s, covering all **256** selections and
  comparing actual services, volumes, networks and the sole HTTPS publication with the catalog.
  This parsed configuration without starting services, pulling images or building images.
- `git diff --check` passed. No backend/frontend source, worker templates, broker definitions,
  certificate/trust stores, router settings or running application services changed in this chunk.
  No packages, tools, images or model assets were installed or downloaded.

**Still pending**

- Worker inclusion/identities, queues, producer gates and real jobs; Off/Effnet/MAEST selection
  and legacy migration; metadata duties without ML; model preparation; storage sizing and
  measured resources; populated monitoring dashboards. Existing fast/deep choices and their
  known dispatch limitations remain unchanged. Resource figures remain provisional estimates.
- Full setting-to-consumer acceptance remains open. The current bindings, deliberate Compose
  differences and unresolved gaps are recorded in [the inventory](installer-capability-inventory.md).
- Earlier P3 permanent client trust and expanded manual acceptance remain pending.

## 2026-10-11 — P3 LAN transport checkpoint

The single-port LAN HTTPS path and its tested browser flows work. **This is not full installer
acceptance, and P3 remains open** pending permanent client trust checks and a broader manual
matrix.

Implemented one-port LAN HTTPS with generated or imported certificates, guarded binding
repair, origin-derived service URLs, protected proxy routes, and scoped endpoint verification.

**Verified**

- Ordinary Builder suite: **435 passed, 0 failed, 5 skipped**. Final serial build: **0 warnings,
  0 errors**. Four Docker checks passed in **2m 20s**, including P2 coverage and explicit Grafana
  cookie assertions. Automated coverage included all **256** feature combinations, a four-case
  cached Nginx matrix, scoped trust and rejection cases, SPA/deep links, auth and secure cookies,
  CORS, presigned image upload, encrypted file round-trip, S3 signature/tamper checks, and
  Grafana login/assets/websocket. A corrected HTTPS fixture passed **1/1 in 59s**, with real API
  health checks before and after transport.
- On PC, refresh, login, and logout worked. On mobile, sign-in after password change and file
  download plus upload-then-download worked smoothly. Grafana was reachable; no dashboards or
  metrics were available. The repo has provisioning YAML only, no dashboard JSON; the standalone
  Grafana proxy check intentionally omitted monitoring and log services. Dashboard provisioning
  is a P4 gap.
- Frontend builds passed with both flag settings. Ten TypeScript errors and lint results of
  21 errors/339 warnings are the unchanged source baseline. No TypeScript source changed.

**Scope and pending**

- The private preview used only `192.168.0.106:10443`, a protected copy of the existing
  certificate, disposable data, current outputs, and cached runtimes. Expected self-signed
  warnings appeared; the existing certificate pair was used without changing OS/browser trust.
  Permanent CA trust and expanded device/browser checks remain unverified.
- The first preview enabled wizard-default optional workers that were absent, showing three
  Down statuses. The corrected fixture disables those workers and matches frontend flags; the
  existing browser instance kept its original flags until cleanup. Repeating a successful initial
  password change still returns HTTP 500, confirmed as the existing already-changed guard.
- Preview cleanup completed: containers, networks, volumes, temporary session/target, and LAN
  listener were removed. Full source Dockerfile builds, broader monitoring/processors and
  browser coverage, and restart/repair acceptance remain for later milestones. Work stops here
  before P4, as requested.

No packages, images, or runtimes were installed or downloaded, and no image was built or pulled.

## 2026-10-11 — P2 validated; work paused at the milestone

P2 implementation and its agreed verification gates are complete. Work stops here for this
session; P3 has not started.

**Verified**

- Full ordinary Builder regression: **331 passed, 0 failed**; one opt-in Docker test was
  skipped. This run included 17 actual Docker CLI parser cases with synthetic dotenv values.
- Serial Builder solution build: **0 warnings, 0 errors**. Actual terminal Ctrl+C and SIGINT
  checks both exited cleanly with code **130**.
- Backend unit suite: **780/780 passed**. Backend solution build: **0 errors, 13
  warnings**. Focused admin/readiness tests: **25/25 passed**.
- Opt-in live Docker fixture: **1/1 passed in 32 seconds**. It covered fresh infrastructure
  initialization; database and exporter authentication; broker identities; atomic `0600`
  credential handoff; Garage key, CORS, and storage use; persistence/readback and handoff
  deletion; repeat initialization with stable keys and no key leakage; and resume from reloaded
  `.env` without a handoff. Core containers remained unchanged, checkpoints agreed, and the
  fixture cleaned up successfully.
- The live fixture used cached helper runtimes and the current `scripts/garage-init/init.sh`
  mounted read-only. It built or pulled no images and published no host ports.

**Implemented in P2**

- Effective configuration preserves existing secrets and unknown settings, safely quotes
  Compose values, and stores versioned non-secret metadata with protected legacy recovery.
- Stable project and volume ownership checks protect existing data, reject missing volumes,
  and prevent generated passwords from replacing established Grafana credentials.
- Staged bootstrap validates configuration and readiness, treats admin bootstrap failure as
  fatal, and avoids tearing down existing services on cancellation.
- Storage key handoff is atomic and restricted, survives persistence errors, supports
  idempotent initialization and resume, and does not log key material.

**Still pending**

- P3 single-port HTTPS and LAN access; P4 capability, model, and storage coverage; P5 final
  diagnostics, readiness, and recovery experience; and P6 image-build/AOT and end-to-end
  acceptance.
- Human terminal visual review and Android/PC trust, sign-in, upload, restart, and repair checks.
- The live fixture is not a production deployment or full installer acceptance.

No tools, packages, or container images were installed or downloaded for this milestone. No
production deployment was performed.

## 2026-10-11 — P1 checkpoint

- P1 input editing, saved wizard choices, path handling, and preflight were implemented.
- Full Builder suite passed **190/190**; backend build and unit checks passed **755/755** at
  that checkpoint.
- Automated terminal checks covered both themes, navigation, resizing, and cancellation at
  normal and narrow sizes. Human visual review remains pending.
- Linux x64 is the current target. Android and PC devices are available for later LAN checks.

## 2026-09-14 — Planning checkpoint

- The installer completion effort was documented; implementation had not started.
- Builder baseline checks passed **82/82** and the serial build had no warnings or errors.
- Disposable Compose syntax checks passed, but no application stack was started.
