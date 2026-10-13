# Alexandria Builder

Builder is the interactive source-based installer for a private LAN. The initial deployment target is **Ubuntu 24.04 x64 (glibc)** with a local Docker engine and the `docker compose` plugin. Other Linux distributions, Windows, macOS and ARM are not claimed as supported. A remote Docker daemon cannot use the installer's local source/settings mounts.

The native executable has completed fresh core and maximal Effnet/MAEST installations from clean source on the Ubuntu 24.04 x64 host. All 11 source images built successfully. The maintainer confirmed PC/Android file transfer, streaming, encryption, previews and Grafana metrics, and accepts manual client trust of automatically generated certificates. The preview has a deferred Loki log-ingestion failure despite passing readiness. P6 delivery is still open; see the [handoff status and limits](../Docs/installer-release-notes.md). No public binary download or image-registry release is assumed.

## Run from source

From the complete repository checkout, with the .NET 10 SDK and the project's existing restored dependencies:

```bash
dotnet run --project 'Builder/Builder.csproj' --no-restore
```

Keep the whole checkout: Builder's project links `Backend/Alexandria.Common/Auth/AccountValidationRules.cs`, uses `Builder/Directory.Packages.props`, and needs the source assets for Docker builds and runtime bind mounts. Copying only `Builder.csproj` is insufficient. The installer checks prerequisites but does not install tools or clone source.

The wizard asks for separate source and installation settings folders, six feature toggles plus Off/Effnet/MAEST, storage capacity, one HTTPS address, certificates and an administrator account. Uploaded files/database state live in Docker-managed named volumes, outside the settings folder. Select that same settings folder on rerun to preserve the project's credentials, volumes and trust identity.

Preselect an established installation explicitly, including when launching from another folder:

```bash
dotnet run --project 'Builder/Builder.csproj' --no-restore -- --settings '/absolute/settings/folder'
# Native bundle:
'/absolute/installer/Builder' --settings '/absolute/settings/folder'
```

The saved source folder is preselected too. Normal inspection and review still precede any
deployment. For a directory move, follow the [relocation procedure](../scripts/installer/README.md);
Docker container mounts and ownership labels must migrate with the settings.

Audio analysis defaults to verified **existing** model files. The Audio models screen also offers an explicit **Download missing or corrupt models (HTTPS)** choice. It reuses verified local assets, downloads only required replacements from `essentia.upf.edu`, and verifies pinned sizes/SHA-256 before atomic promotion. Transfers begin during installation after the summary, support cancellation/retry, and need no Python or new host dependencies. The choice is saved for repair and can be changed back to offline. Off skips model preparation entirely. Effnet needs 17 assets totaling 24,056,930 bytes; MAEST needs 17 totaling 370,003,909 bytes, including shared Effnet embeddings/mood heads. Image builds may need registry/package-repository access unless all dependencies are already cached; lyrics lookup requires outbound HTTPS to `lrclib.net`.

## Native publication with existing Ubuntu packs

Native AOT needs the target runtime/compiler packs and system build tools already available. The following offline commands match the existing Ubuntu 24.04 x64 packs and GCC toolchain used for publication. The generic `linux-x64` RID can require different packs and is not interchangeable with this command.

From the repository root, choose an empty local feed and output directory:

```bash
alexandria_empty_feed='/tmp/alexandria-empty-feed'
alexandria_publish='/tmp/Alexandria Builder native'

mkdir -p "$alexandria_empty_feed"

dotnet restore 'Builder/Builder.csproj' \
  --source "$alexandria_empty_feed" --ignore-failed-sources \
  -r ubuntu.24.04-x64 -p:UseRidGraph=true -p:NuGetAudit=false -m:1

dotnet publish 'Builder/Builder.csproj' \
  -c Release -r ubuntu.24.04-x64 --no-restore \
  -p:UseRidGraph=true -p:CppCompilerAndLinker=gcc -m:1 \
  -o "$alexandria_publish"
```

An empty feed prevents fetching packages; restore will fail if required dependencies are absent from the existing cache. It does not install missing native tools. Keep the published `Builder` executable together with the complete matching `Templates/Local` tree, and retain the full source checkout separately. A native executable needs the target system's native libraries but no .NET runtime.

Run the bundle from a terminal, including from a different current directory:

```bash
'/absolute/path/to/Alexandria Builder native/Builder'
```

Point the wizard to the real source and settings folders. Changing the current directory must not substitute for shipping the templates. Publishing an executable is only one gate: verify actual execution, template discovery and installation behavior on the claimed host before distribution.

## Completion and operations

`INSTALLATION.txt` records the exact HTTPS address, project/volume identity, log locations and correctly quoted management commands. `nginx/CLIENT-TRUST.txt` records the public certificate and fingerprints. Trust only the public CA/certificate on clients; keep `.env` and private keys protected. Server readiness does not prove a second device trusts the certificate.

Use `stop` for ordinary downtime and `up -d --no-build --pull never` with the saved explicit project arguments to restart. `down -v` deletes data volumes. Repair/retry retains authoritative settings and already-started services; cancellation stops child work and can leave services running. Back up the complete settings folder **and all named volumes** consistently. There is no automatic backup scheduler or database/schema downgrade rollback.

User guides:

- [Installation](../Docs/src/content/docs/getting-started/installation.mdx)
- [First steps and client trust](../Docs/src/content/docs/getting-started/first-steps.mdx)
- [Operations, named failures, backup and restore](../Docs/src/content/docs/guides/operations.mdx)
- [Separate reference Compose workflow](../Docs/src/content/docs/getting-started/manual-installation.mdx)

Developer hot reload remains the separate `run-dev.sh` / `docker-compose.dev.yml` workflow. Test and acceptance commands are in [the integration-test guide](Builder.Tests/Integration/README.md).
