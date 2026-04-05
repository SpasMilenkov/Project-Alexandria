# Project Alexandria
*Self-hosted file management with versioning, previews, and access control.*

![Status](https://img.shields.io/badge/status-pre--alpha-red)

> [!CAUTION]
> This project is pre-alpha software. Do not use it to store files you care about —
> data loss, breaking changes, and instability are expected at this stage.

## Features

### File & Directory Management
- **File Operations** — Upload, download, version, and soft-delete/restore with one-click recovery
- **Preview Generation** — Async previews for images, documents, audio, video, archives, and text
- **Directory Trees** — Nested directories with full CRUD, copy/move, and search
- **Tagging** — Custom tags with colors and icons; tag-based search and filtering

### Users & Access
- **Authentication** — JWT-based auth with refresh tokens and secure onboarding flow
- **Role-Based Access** — Granular permissions with `User` and `Admin` roles (RBAC)
- **Admin Dashboard** — User registry, upload policy config, and system health monitoring

### Insights & Logging
- **Audit Logging** — Comprehensive activity tracking with calendar visualization
- **Storage Metrics** — Usage tracking and automatic content deduplication

### Interface & Experience
- **Theming** — Dark/light mode, accent colors, background images, and custom themes
- **PWA Support** — Offline-capable via service workers for a native app feel

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core (C#), FastEndpoints |
| Frontend | Vue 3, TypeScript, Vite, Tailwind CSS, Pinia |
| Database | PostgreSQL 16 |
| Object Storage | S3-compatible (Garage, MinIO, or RustFS) |
| Message Broker | RabbitMQ |
| Monitoring | Prometheus + Grafana |

## Prerequisites

- Docker & Docker Compose
- Node.js 20.19+ or 22.12+ (for local development)
- .NET 10 SDK (for local development)

## Quick Start

### Production

```bash
# Clone the repository
git clone https://github.com/your-org/Project-Alexandria.git
cd Project-Alexandria

# Copy and configure environment
cp .env.template .env
# Edit .env with your values

# Start all services
docker compose up -d
```

The application will be available at `http://localhost:3001`.

### Development

```bash
# Start infrastructure services
docker compose -f docker-compose.dev.yml up -d

# Launch all services via tmux
./run-dev.sh --s3-provider Garage
```

Development services:

| Service | URL |
|---------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:5000 |
| RabbitMQ Management | http://localhost:15672 |
| Garage S3 | http://localhost:9000 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 |

## Configuration

### Environment Variables

See `.env.template` for the full list of required and optional variables. Key variables:

| Variable | Description |
|----------|-------------|
| `DB_PASSWORD` | PostgreSQL password |
| `MINIO_ROOT_USER` | MinIO admin username |
| `MINIO_ROOT_PASSWORD` | MinIO admin password |

### S3 Providers

The project supports multiple S3-compatible storage backends:

- **Garage** (default) — Lightweight, Rust-based
- **MinIO** — Full-featured, S3-compatible
- **RustFS** — Alternative Rust-based option

Select via `--s3-provider` flag when running `run-dev.sh`.

## Testing

### Backend

```bash
cd Backend
dotnet test
```

### Frontend

```bash
cd Frontend
pnpm test:unit    # Unit tests (Vitest)
pnpm test:e2e     # E2E tests (Playwright)
```

## Status & Roadmap

Core file operations and authentication are functional. Preview generation and the admin dashboard are under active development. The project is not yet feature-complete and has no stable release.

See the [issue tracker](https://github.com/your-org/Project-Alexandria/issues) for planned work and known issues.

## Contributing

Issues and pull requests are welcome. There are no formal contribution guidelines yet — just open an issue before starting significant work so effort isn't duplicated.

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE).
