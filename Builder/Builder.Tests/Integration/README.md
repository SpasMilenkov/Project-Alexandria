# Opt-in Docker verification

Ordinary Builder test runs skip tests tagged `Category=Docker`. To enable them on a machine
with Docker access and the required images already cached:

```bash
ALEXANDRIA_BUILDER_DOCKER_TESTS=1 dotnet test Builder/Builder.Tests --no-restore -m:1 --filter Category=Docker
```

The suite never pulls images, builds images, or installs packages. Missing cached images or
application outputs fail with concrete prerequisites. Each test generates synthetic credentials
in a unique temporary target and uses a unique Compose project. Cleanup removes only that
fixture's containers, networks, and volumes. Infrastructure tests publish no ports. The HTTPS
test publishes exactly one ephemeral TCP port on `127.0.0.1`; it does not deploy to the LAN.

Required cached images:

- `postgres:16-alpine`
- `rabbitmq:4.2.5-management-alpine`
- `dxflrs/garage:v2.2.0`
- `alpine:3.20`
- `alexandria-local-garage-init:latest`

The Garage helper uses its cached runtime with the checkout's current `init.sh` mounted
read-only. This verifies infrastructure initialization and recovery using the current script;
it does not establish that the source Dockerfile builds successfully.

HTTPS tests additionally require these cached runtimes:

- `alexandria-local-api:latest`
- `alexandria-local-frontend:latest`
- `alexandria-local-nginx:latest`
- `alexandria-local-media-worker:latest`
- `alexandria-local-playlist-worker:latest`
- `grafana/grafana:12.4.2`
- `prom/prometheus:v3.13.2`
- `grafana/loki:3.7.6`
- `grafana/alloy:v1.18.1`
- `ghcr.io/google/cadvisor:v0.60.5`
- `prometheuscommunity/postgres-exporter:v0.20.1`

The live HTTPS test requires current compiled API, media and playlist worker output at each
project's `bin/Debug/net10.0` and a current frontend production bundle at
`Frontend/dist`. Prepare them with existing cached prerequisites before opting in. The API
assemblies and frontend bundle are mounted read-only over the cached runtimes. Application
fixtures mount empty app settings in a separate configuration directory so generated Compose
bindings must work without ignored local settings. No cached image's old application bundle
is accepted as evidence for current source behavior. Monitoring images must be cached under
the exact digest references in the template; the list above identifies their version tags.

`HttpsTransportTests` parses all 192 supported selections (six toggles and Off/Effnet/MAEST),
comparing actual service, volume and network inventories with the resolved capability catalog
and checking one published HTTPS port. It
uses the cached Nginx image to validate generated/imported synthetic certificates with
monitoring enabled and disabled. Its isolated runtime test verifies the current SPA and deep
routes, secure authentication cookies and refresh/logout, origin-specific CORS, API-presigned
image uploads and encrypted file round trips, escaped S3 keys/query signatures, protected
asset rejection, and Grafana login/assets/live websocket connections through a custom port.
It starts the complete monitoring stack, checks all three provisioned dashboards and authenticated
datasource queries for eight healthy scrapes and real API logs through the HTTPS proxy.
The encrypted payload follows the frontend's AES-GCM/PBKDF2 protocol; hashing uses the
already compiled API's Blake3 runtime without adding Builder package dependencies.

The fixture uses a synthetic public IP in URLs and certificate SANs. Only its private HTTP
handler maps transport to the dedicated loopback port. Trust is scoped to that handler's
generated CA, with explicit rejection tests for an untrusted certificate and a wrong hostname.
The OS trust store is unchanged. The transport fixture explicitly disables optional processing
features rather than inheriting the wizard's enabled defaults. It checks the API's actual `/health` endpoint inside its container
before and after the round trip, independently of Nginx's public health response. This prevents
unstarted optional workers from creating false outage reports in the core-only preview.
The separate monitoring fixture executes every dashboard query and verifies project-scoped
container metrics and plain/JSON log ingestion. Worker and inference fixtures test selected processors.
Android/PC trust, browser crypto execution, second-device LAN flows, source image builds,
restart, and full installer acceptance remain separate manual or later-phase checks.

## Manual LAN browser preview

`Category=ManualLan` is skipped unless both `ALEXANDRIA_BUILDER_DOCKER_TESTS=1` and
`ALEXANDRIA_BUILDER_LAN_CHECK=1` are set. This separate fixture binds one explicitly selected
host IPv4 address and unused HTTPS port, uses disposable project data, and performs the real
installer HTTPS verification before exposing its session details. It uses the same cached
runtimes and current application outputs as the automated tests.

Set `ALEXANDRIA_BUILDER_LAN_HOST`, `ALEXANDRIA_BUILDER_LAN_PORT`, and
`ALEXANDRIA_BUILDER_LAN_SESSION` (a new, absolute temporary JSON path), then run:

```bash
dotnet test Builder/Builder.Tests --no-build --no-restore -m:1 --filter Category=ManualLan
```

The public host may also be a DNS name resolving to the machine. In that case set
`ALEXANDRIA_BUILDER_LAN_BIND_ADDRESS` to its actual LAN IPv4 interface. A different public
hostname separates browser cookies from another installation: different ports on the same
hostname do not separate cookie scopes. Never invent an unreachable IP for the certificate.

To reuse an existing certificate, also set `ALEXANDRIA_BUILDER_LAN_CERTIFICATE` and
`ALEXANDRIA_BUILDER_LAN_PRIVATE_KEY` to the existing PEM files. Otherwise the fixture creates
its own local CA. The session file is written with mode `0600` and includes the preview URL,
temporary sign-in credentials, public trust file/fingerprint, and `ReleasePath`. It appears
only after the endpoint is ready. Do not commit or share the session file.

Check certificate trust, sign-in, file upload/download, a refreshed SPA deep link, sign-out,
public API, and Grafana login/assets in the actual PC/Android browsers. Record device results
separately: the fixture cannot determine whether a person completed browser acceptance.
Creating the session's `ReleasePath` file ends the preview; cancellation or the 30-minute
deadline also removes its disposable containers, networks, and volumes. The session file and
target are deleted on normal cleanup. No existing deployment, firewall, router, or client trust
store is changed.

Generated credentials and raw Docker output stay in memory or protected fixture files.
Assertions avoid including credential values, and command failure diagnostics pass through
the production secret redactor.

## Cached model and inference broker acceptance

`ModelRuntimeTests` runs both current inference backbones in a cached classifier image with
the current Python files and pinned requirements mounted read-only. Set
`ALEXANDRIA_EXISTING_MODEL_SOURCE` to an existing model cache; no model assets are downloaded.
Each backbone receives a fresh verified 17-file subset and a generated 31-second WAV.
Network access is disabled for this standalone inference check. Both images must run as
UID/GID 1001, share the named data volume, and retain the classifier's 2 GiB memory limit.

`InferenceBrokerTests` additionally requires current compiled metadata-coordinator output
at `Backend/Alexandria.Workers.MediaMetadata/bin/Debug/net10.0`, the processing runtimes used
by `CachedWorkerProcessingFixture`, and the infrastructure images listed above. It runs
Effnet and MAEST in separate disposable projects with no host ports. Each test uploads its
generated WAV to Garage, publishes a real enrichment trigger, and verifies coordinator
staging/dispatch, the selected classifier consumer, completed batch/job rows, genre and mood
enrichments, model versions, delivery acknowledgements and shared-file cleanup. The omitted
backbone has no consumer. Current application health endpoints and actual Docker healthchecks
must pass; the fixture does not replace them with successful commands.

Additional cached model runtimes:

- `alexandria-local-essentia-effnet-worker:latest` (the common image supports either backbone)
- `alexandria-local-media-metadata-worker:latest`

To run only these checks after compiling with cached prerequisites:

```bash
ALEXANDRIA_BUILDER_DOCKER_TESTS=1 ALEXANDRIA_EXISTING_MODEL_SOURCE=/absolute/path/to/existing/models \
  dotnet test Builder/Builder.Tests --no-build --no-restore -m:1 \
  --filter 'FullyQualifiedName~ModelRuntimeTests|FullyQualifiedName~InferenceBrokerTests'
```

The Python-only standalone wrapper can also run through standard-library unittest discovery:

```bash
ALEXANDRIA_BUILDER_MODEL_RUNTIME_TESTS=1 ALEXANDRIA_EXISTING_MODEL_SOURCE=/absolute/path/to/existing/models \
  python3 -m unittest discover -s classificators/essentia/tests -p test_cached_runtime.py -v
```

These checks validate current model preparation, inference and broker integration using cached
runtimes. Building the source Dockerfiles and completing an installer lifecycle remain separate
acceptance gates. Recorded memory peaks are fixture measurements, not universal maxima.

## Garage capacity and lyrics checks

`GarageCapacityRuntimeTests` uses the actual cached Garage v2.2.0 runtime and current init
script. It creates a 1 decimal GB layout, checks the 1,000,000,000-byte receipt, stores an
object, and reruns initialization with a stale 10 GB default. The actual capacity and object
must survive. A reviewed request for 2 GB must fail without changing either. It uses only the
infrastructure prerequisites above and publishes no ports.

```bash
ALEXANDRIA_BUILDER_DOCKER_TESTS=1 dotnet test Builder/Builder.Tests --no-build --no-restore -m:1 \
  --filter FullyQualifiedName~GarageCapacityRuntimeTests
```

The lyrics processing check requires outbound HTTPS to lrclib.net. It first forces a
fixture-only unavailable HTTPS proxy and requires a real counted retry/failure with lyrics
still pending. It then restores the provider, restarts the worker, and requests a unique
nonexistent track. A successful lookup must persist the provider's no-match outcome and
acknowledge the broker delivery as `lyrics_worker_user`. An outage cannot count as a
successful no-match lookup.

## Deployment lifecycle and repair checks

After building Builder with the existing .NET cache:

```bash
dotnet build Builder/AlexandriaBuilder.slnx --no-restore -m:1
dotnet test Builder/Builder.Tests --no-restore --no-build --filter 'Category!=Docker&Category!=ManualLan'
ALEXANDRIA_BUILDER_DOCKER_TESTS=1 dotnet test Builder/Builder.Tests --no-restore --no-build --filter 'FullyQualifiedName~AdminLoginRuntimeTests|FullyQualifiedName~RecoveryRuntimeTests'
python3 Builder/Builder.Tests/Terminal/run_terminal_smoke.py
```

`AdminLoginRuntimeTests` uses the production certificate-pinned verifier against a disposable
HTTPS installation, verifies the fresh administrator role/identity and authenticated profile,
rejects an incorrect password, and confirms the original password still works. The current
API has no active CSRF challenge/header exchange; the verifier uses the actual same-origin
cookie protocol, including any issued CSRF cookie. It never resets an account password.

`RecoveryRuntimeTests` uploads and decrypts an encrypted file through the real API, restarts
the owned services, stops a selected worker, requires production readiness to identify that
exact failure, then repairs through the production orchestrator. It compares generated
artifacts, environment, credentials, certificates, metadata, named-volume attachments,
retained authenticated sessions and stored encrypted bytes. A second disposable installation
must retain its container IDs, start times, restart counts and data attachments. Only the
build/up adapter is replaced: it verifies existing cached images and starts with
`--pull never --no-build`. This is runtime repair evidence; clean source image builds and
native installer delivery remain separate acceptance gates.

The terminal smoke uses Linux `/proc`, Python's standard-library PTY support and the installed
.NET 10 Roslyn/reference pack. It compiles a test-only harness directly against the current
Builder output in a temporary folder; it performs no restore or installation. Real silent
and noisy child processes exercise theme persistence, narrow/wide terminal repaint, Ctrl+C
exit 130, alternate-buffer/cursor restoration and complete child-tree termination. Preferences
and logs stay under the temporary fixture. No Docker resources are used by this smoke.

## Source-built image acceptance

Set `ALEXANDRIA_BUILDER_SOURCE_IMAGES` to an absolute JSON manifest path to test the packaged
applications instead of mounting host application output over cached runtimes. Build the
repository Dockerfiles from a clean source copy first; the fixtures do not build or pull images.
The manifest is an array with one record per successful build:

```json
[
  {
    "name": "api",
    "image": "my-disposable-api:local",
    "image_id": "sha256:<64 lowercase hexadecimal digits>",
    "exit_code": 0
  }
]
```

Supported names are `api`, `frontend`, `nginx`, `garage-init`, `media-worker`, `document-worker`,
`transpilation-worker`, `media-metadata-worker`, `playlist-worker`, `lyrics-worker` and
`essentia`. The common `essentia` image serves either selected inference backbone. Obtain each
immutable ID with `docker image inspect --format '{{.Id}}' <built-image>`; the fixture checks
that the named image still resolves to that ID before starting containers.

Source mode keeps each image's own application files, entrypoint, tools and settings. It
rejects host application overlays and execution overrides; generated production configuration,
verified model assets and data volumes remain mounted. Infrastructure, HTTPS, worker,
monitoring and inference-broker fixtures support this mode. The standalone `ModelRuntimeTests`
still exercises current Python files over a cached runtime and is separate evidence.

After the coordinated Builder build, run the packaged application checks with existing models:

```bash
ALEXANDRIA_BUILDER_DOCKER_TESTS=1 \
ALEXANDRIA_BUILDER_SOURCE_IMAGES=/absolute/path/to/build-results.json \
ALEXANDRIA_EXISTING_MODEL_SOURCE=/absolute/path/to/existing/models \
  dotnet test Builder/Builder.Tests --no-build --no-restore -m:1 \
  --filter 'FullyQualifiedName~Https_customPort_servesCurrentApplicationAuthStorageEncryptionAndMonitoring|FullyQualifiedName~Processing_real_workers_preserve_core_metadata_and_complete_previews_streaming_and_playlist_sync|FullyQualifiedName~Effnet_real_coordinator_dispatches_classifies_persists_and_cleans_shared_files|FullyQualifiedName~Maest_real_coordinator_dispatches_classifies_persists_and_cleans_shared_files|FullyQualifiedName~Lyrics_real_lookup_retries_provider_outage_then_records_not_found_and_acknowledges_delivery'
```

## Published native installer acceptance

Publish the native executable and all templates together as described in
[Builder's build instructions](../../README.md). `Terminal/run_published_smoke.py` launches that
executable from an independent working directory with no .NET runtime path, checks every
shipped template against source, and drives the actual wizard through a PTY. Source and target
paths must contain spaces. Its default cancels before deployment, requiring exit 130 and no
generated installation artifacts. `--deploy` explicitly permits a real installation and leaves
it running; `--repair` explicitly permits updating an existing installer-generated target.

```bash
python3 Builder/Builder.Tests/Terminal/run_published_smoke.py \
  --binary '/absolute/published installer/Builder' \
  --source '/absolute/clean source' --target '/absolute/new test target' \
  --host 192.168.0.106 --port 10443 --selection core
```

Selections are `core`, `recommended`, `max-effnet` and `max-maest`; audio selections require
`--model-source /absolute/path/to/existing/models`. A successful deployment requires the
complete checkpoint, installation summary, deleted Garage credential handoff and matching
non-secret persistence receipt. Repair additionally preserves project identity and certificate
trust material. Use disposable targets and unused ports. This smoke never installs prerequisites
or removes Docker resources. Record actual PC/Android trust and browser checks separately.

Deployment logs are stored as `installer-logs/install-UTCtimestamp-identity.log` under the
selected settings folder (owner read/write on Unix). Commands, stages, elapsed time, exits
and redacted evidence are retained; captured resolved Compose configuration and environment
are excluded. Failure recovery supports phase retry, status, service logs, a scrollable log,
return to summary, or exit while preserving services/data. Retry checks the generated artifact
checkpoint and effective environment, and validates Compose again. Successful installation
writes `INSTALLATION.txt` with address, account verification limits, client-trust instructions,
config/data/model/log locations and quoted project-specific start/stop/status/log commands.
