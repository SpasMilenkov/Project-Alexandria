# Project Alexandria
*Self-hosted file management with versioning, previews, and access control.*

![Status](https://img.shields.io/badge/status-pre--alpha-red)

> [!CAUTION]
> This project is pre-alpha software. Do not use it to store files you care about -
> data loss, breaking changes, and instability are expected at this stage.

## Context
Alexandria is a self-hosted-all-in one solution. It is curretly not intended for commercial use or something facing the internet. It was started as a solution for sharing files accross my different devices on my local networ, then I just kept adding stuff as my needs changed. If you find yourself checking this project out of curiosity feedback is appreciated.

The system was designed to be modular and extendable, so that it could fit the different needs of different people and run on variety of lower end hardware.

## Features

### File & Directory Management
- **File Operations**: Upload, download, version, and soft-delete/restore with one-click recovery
- **Preview Generation**: Async previews for images, documents, audio, video, archives, and text
- **Directory Trees**: Nested directories with full CRUD, copy/move, and search
- **Tagging**: Custom tags with colors and icons; tag-based search and filtering

### Users & Access
- **Authentication**: JWT-based auth with refresh tokens and secure onboarding flow
- **Role-Based Access**: Granular permissions with `User` and `Admin` roles (RBAC)
- **Admin Dashboard**: User registry, upload policy config, and system health monitoring

### Insights & Logging
- **Audit Logging**: Comprehensive activity tracking with calendar visualization
- **Storage Metrics**: Usage tracking and automatic content deduplication

### Interface & Experience
- **Theming**: Dark/light mode, accent colors, background images, and custom themes
- **PWA Support**: Offline-capable via service workers for a native app feel

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core (C#), FastEndpoints |
| Frontend | Vue 3, TypeScript, Vite, Tailwind CSS, Pinia |
| Database | PostgreSQL 16 |
| Object Storage | S3-compatible (Garage, MinIO, or RustFS) |
| Message Broker | RabbitMQ |
| Monitoring | Prometheus + Grafana |

## AI Usage disclaimer

AI has been used in the development to some extend. Mainly for writing tests and CSS.

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE).

