# 📚 Alexandria - Development Setup

> Self-hosted digital library for documents, media, and web content

## 🚀 Quick Start

```bash
git clone <your-repo-url> alexandria
cd alexandria
cp .env.example .env
docker-compose -f docker-compose.dev.yml up -d
```

## 🔗 Development URLs

| Service              | URL                                            | Purpose                   |
| -------------------- | ---------------------------------------------- | ------------------------- |
| 🐘 **PostgreSQL**    | `localhost:5432`                               | Direct database access    |
| 🗄️ **MinIO API**     | [http://localhost:9000](http://localhost:9000) | S3-compatible storage API |
| 🎛️ **MinIO Console** | [http://localhost:9001](http://localhost:9001) | Storage management UI     |

## 🔑 Default Development Credentials

### MinIO

```
URL: http://localhost:9000
Username: dev_admin
Password: dev_password_123
```

### PostgreSQL

```
Host: localhost
Port: 5432
Database: alexandria_dev
Username: alexandria_user
Password: dev_password_123
```

## 💾 Database Connection

```bash
# Connect directly
psql -h localhost -p 5432 -U alexandria_user -d alexandria_dev

# Or with connection string
psql "postgresql://alexandria_user:dev_password_123@localhost:5432/alexandria_dev"
```

**GUI connection string:**

```
postgresql://alexandria_user:dev_password_123@localhost:5432/alexandria_dev
```

## 🗂️ MinIO Buckets (Auto-Created)

| Bucket                   | Purpose              | Access              |
| ------------------------ | -------------------- | ------------------- |
| `alexandria-files`       | Main file storage    | Private             |
| `alexandria-thumbnails`  | Generated previews   | Public read         |
| `alexandria-temp`        | Temporary processing | Auto-expire (1 day) |
| `alexandria-dev-testing` | Development testing  | Public read         |

## 🛠️ Development Commands

```bash
# Start all services
docker compose -f docker-compose.dev.yml up -d

# View logs
docker compose -f docker-compose.dev.yml logs -f

# Stop services
docker compose -f docker-compose.dev.yml down

# Reset all data
docker compose -f docker-compose.dev.yml down -v
```

### Database Operations

```bash
# Connect into container
docker exec -it alexandria-postgres-dev psql -U alexandria_user -d alexandria_dev
```

### MinIO Operations

```bash
# List buckets
docker exec -it alexandria-minio-dev mc ls alexandria/
```

## 🐛 Troubleshooting

```bash
# Check service logs
docker-compose -f docker-compose.dev.yml logs postgres
docker-compose -f docker-compose.dev.yml logs minio
```

## 📁 Project Structure

```
alexandria/
├── docker-compose.dev.yml     # Development services
├── docker-compose.prod.yml    # Production services
├── .env.example               # Environment template
├── .env                       # Your local config (gitignored)
└── README.md                  # This file
```?
