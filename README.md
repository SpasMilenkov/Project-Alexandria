# Project Alexandria
*Self-hosted file management with versioning, previews, and access control.*

![Status](https://img.shields.io/badge/status-pre--alpha-red)

> [!CAUTION]
> This project is pre-alpha software. Do not use it to store files you care about -
> data loss, breaking changes, and instability are expected at this stage.

## Context

Alexandria is a self-hosted, all-in-one solution. It is currently not intended for commercial use or for facing the internet. It started as a way to share files across my different devices on my local network, and then I just kept adding stuff as my needs changed. If you find yourself checking this project out of curiosity, feedback is appreciated.

The system was designed to be modular and extensible, so that it could fit the different needs of different people and run on a variety of lower-end hardware.

## Features

### Files and organization
- **Files and versioning**: upload, download, keep versions, soft-delete and restore from a trash view
- **Directories**: nested directory trees with copy/move and search
- **Uploads and previews**: asynchronous previews for images, documents, audio, video, archives and text
- **Sharing links**: share files through links that work without an account
- **Tags**: custom tags with colors and icons, tag-based search and filtering
- **Folder policies**: per-directory rules that run automatically on new uploads and versions
- **Document text search**: text is extracted from documents and made searchable
- **Client-side encryption**: optional, files are encrypted in the browser before upload

### Notes and gallery
- **Notes**: a rich-text editor with autosave, links between notes and files, backlinks and embedded media
- **Gallery**: a photo timeline built from image metadata such as capture date and location

### Music and video
- **Streaming**: music and video playback with a global player, queue and history
- **Playlists**: create, edit and favorite playlists
- **Lyrics**: fetched from an external provider (needs outbound HTTPS)
- **Transcoding**: adaptive streaming conversion, with optional VAAPI GPU encoding for video
- **Listening stats**: timelines, top tracks, and monthly and annual "Wrapped" recaps
- **Audio analysis**: optional automatic tagging with Essentia (EffNet or MAEST models)

### Administration
- **Users and roles**: JWT authentication with refresh tokens, `User` and `Admin` roles, onboarding flow
- **Audit log**: activity history with a calendar view and export
- **Admin dashboard**: user registry, storage overview, system configuration and integration settings
- **Monitoring**: service status, incidents, resource usage and a public status page, with optional Prometheus/Grafana/Loki dashboards

### Interface
- **Theming**: dark/light mode, accent colors and background images
- **PWA**: installable, with a service worker

## Quick start

The recommended way to install Alexandria is **Builder**, an interactive terminal installer for a private LAN. It checks prerequisites, builds the images from your source checkout, generates settings and certificates, and verifies the selected services.

Requirements: a full checkout of this repository, Docker with the `docker compose` plugin, and the .NET 10 SDK. Ubuntu 24.04 x64 is the only tested target.

```bash
git clone https://github.com/SpasMilenkov/Project-Alexandria.git
cd Project-Alexandria
dotnet run --project 'Builder/Builder.csproj' --no-restore
```

If the project has not been restored yet, drop `--no-restore`. To reopen an existing installation, pass `-- --settings '/absolute/settings/folder'`.

See the [Builder README](Builder/README.md) and the [installation guide](Docs/src/content/docs/getting-started/installation.mdx) for details. For the reference Compose stack (`docker-compose.local.yml`) without Builder, read the [manual installation guide](Docs/src/content/docs/getting-started/manual-installation.mdx). Developer hot reload uses `run-dev.sh` and `docker-compose.dev.yml` instead ([development setup](Docs/src/content/docs/guides/set-up-development-environment.mdx)).

## Documentation

The documentation is an [Astro Starlight](https://starlight.astro.build/) site in `Docs/`. To run it locally:

```bash
cd Docs
pnpm install
pnpm dev
```

Useful starting points:

- Getting started: [introduction](Docs/src/content/docs/getting-started/introduction.mdx), [installation](Docs/src/content/docs/getting-started/installation.mdx), [first steps](Docs/src/content/docs/getting-started/first-steps.mdx)
- Guides: [files and versioning](Docs/src/content/docs/guides/files-and-versioning.mdx), [uploads and previews](Docs/src/content/docs/guides/uploads-and-previews.mdx), [sharing links](Docs/src/content/docs/guides/sharing-links.mdx), [tags and auto-tagging](Docs/src/content/docs/guides/tags-and-auto-tagging.mdx), [access policies](Docs/src/content/docs/guides/access-policies.mdx), [streaming and playlists](Docs/src/content/docs/guides/streaming-and-playlists.mdx), [encryption](Docs/src/content/docs/guides/encryption.mdx)
- Operations: [backup and restore](Docs/src/content/docs/operations/backup-and-restore.mdx), [configuration reference](Docs/src/content/docs/operations/configuration-reference.mdx), [monitoring dashboards](Docs/src/content/docs/operations/monitoring-dashboards.mdx), [troubleshooting](Docs/src/content/docs/operations/troubleshooting.mdx)
- Development: [architecture](Docs/src/content/docs/development/architecture.mdx), [project structure](Docs/src/content/docs/development/project-structure.mdx), [workers](Docs/src/content/docs/development/workers.mdx), [testing](Docs/src/content/docs/development/testing.mdx), [contributing](Docs/src/content/docs/development/contributing.mdx)

## Tech stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core, FastEndpoints, EF Core with Npgsql |
| Frontend | Vue 3, TypeScript, Vite, Nuxt UI v4, Tailwind CSS v4, Pinia Colada |
| Database | PostgreSQL 16 |
| Object storage | Garage (S3-compatible) |
| Message broker | RabbitMQ 4 |
| Workers | Six .NET workers: document, media, media metadata, media transpilation, playlist and lyrics |
| Audio analysis | Essentia classifier containers (EffNet or MAEST), optional |
| Monitoring | Prometheus, Grafana, Loki, Alloy, cAdvisor and postgres-exporter, optional |
| Reverse proxy | nginx |
| Installer | Builder, a .NET 10 terminal UI |

## Repository layout

| Folder | Contents |
|--------|----------|
| `Backend/` | .NET solution: API, data and repository layers, domain services and workers |
| `Frontend/` | Vue single-page app |
| `Builder/` | Setup TUI (installer) |
| `Docs/` | Documentation site (Astro Starlight) |
| `classificators/` | Essentia audio analysis containers and model requirements |
| `scripts/` | Installer relocation helper, storage and database init scripts, worktree and sandbox helpers |
| `nginx/`, `prometheus/`, `grafana/`, `loki/`, `alloy/`, `rabbitmq/` | Configuration for the services in the Compose files |
| `plans/` | Living planning documents for features in progress |
| `skills/` | Guidance for AI coding agents and contributors, entry point is `AGENTS.md` |

Compose files at the root: `docker-compose.local.yml` (manually maintained reference stack) and `docker-compose.dev.yml` (development).

## AI usage disclaimer

AI has been used in the development of this project.

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE).
