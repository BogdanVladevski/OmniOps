# OmniOps

> Distributed real-time vehicle telemetry platform

![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet)
![Kafka](https://img.shields.io/badge/Apache_Kafka-231F20?logo=apachekafka)
![Redis](https://img.shields.io/badge/Redis-DC382D?logo=redis)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql)
![React Native](https://img.shields.io/badge/React_Native-61DAFB?logo=react)
![SignalR](https://img.shields.io/badge/SignalR-512BD4)
![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker)
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-000000?logo=opentelemetry)

OmniOps ingests vehicle GPS telemetry through Apache Kafka, processes it with a .NET 9 backend (CQRS + MediatR), persists history in PostgreSQL, caches hot state in Redis, and streams live updates to an Expo mobile client over SignalR.

The backend uses **Clean Architecture** with four layers, environment-driven configuration, and production-style reliability patterns: transactional outbox, idempotent consumption, and a dead-letter queue.

**Deep dive:** [How a GPS packet becomes fleet intelligence →](https://bogdanvladevski.github.io/OmniOps/)

**Local setup:** [DEV-SETUP.md](DEV-SETUP.md)

---

## How it works

```mermaid
flowchart LR
    A[Simulate API] --> B[(fleet-telemetry)]
    B --> C[Kafka Consumer]
    C --> D[MediatR]
    D --> E[(PostgreSQL)]
    D --> F[(Redis)]
    D --> G[SignalR Hub]
    G --> H[Expo App]
    E --> I[Outbox]
    I --> J[Outbox Worker]
    J --> K[(fleet-telemetry-events)]
```

```mermaid
sequenceDiagram
    participant API as Simulate API
    participant Kafka as Kafka
    participant Worker as Kafka Consumer
    participant App as MediatR
    participant DB as PostgreSQL
    participant Redis as Redis
    participant Outbox as Outbox Worker
    participant Hub as SignalR
    participant Mobile as Expo App

    API->>Kafka: VehicleTelemetry JSON
    Kafka->>Worker: Consume
    Worker->>App: ProcessTelemetryCommand
    App->>Redis: Dedup lock
    App->>DB: Persist + domain event
    App->>Redis: Update latest cache
    App->>Hub: Broadcast to vehicle group
    Hub->>Mobile: ReceiveTelemetryUpdate
    Outbox->>Kafka: Domain events
```

1. The **simulate endpoint** (or any external producer) publishes `VehicleTelemetry` JSON to `fleet-telemetry`.
2. **KafkaTelemetryConsumer** deserializes the message and dispatches `ProcessTelemetryCommand` via MediatR.
3. The handler deduplicates via Redis, persists to PostgreSQL, updates the cache, runs anomaly detection, and broadcasts over SignalR.
4. Domain events are captured by an **EF outbox interceptor** in the same database transaction and later published to `fleet-telemetry-events` by **OutboxPublisherWorker**.

---

## Features

| Area | Implementation |
|------|----------------|
| Event streaming | Kafka consumer with retry, backoff, and DLQ routing |
| CQRS | MediatR commands, queries, logging/validation pipeline behaviours |
| Outbox | `OutboxSaveChangesInterceptor` + polling publisher |
| Idempotency | Redis `SET NX` per packet ID (10-minute TTL) |
| Dead letter queue | Unparseable messages → `fleet-telemetry-dlq` |
| Cache | Redis latest-telemetry per vehicle |
| Anomaly detection | 30-second sliding window (fuel + engine thermal) |
| Playbook orchestration | Simulated RAG incident response via SignalR |
| Real-time | SignalR vehicle + fleet groups, camelCase JSON |
| Observability | Serilog structured logging, OpenTelemetry tracing, Prometheus `/metrics` |
| Mobile | Expo fleet dashboard — live map, KPIs, anomaly alerts |

---

## API

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/telemetry/fleet` | Latest cached telemetry for all configured fleet vehicles + summary KPIs |
| `GET` | `/api/telemetry/{vehicleId}` | Latest cached telemetry (Redis) |
| `POST` | `/api/test/simulate/{vehicleId}?packets=N` | Publish mock telemetry to Kafka (`N` = 1–100) |
| `GET` | `/health/live` | Liveness probe (process up) |
| `GET` | `/health/ready` | Readiness probe (Postgres, Redis, Kafka) |
| `GET` | `/metrics` | Prometheus metrics (when enabled) |
| WS | `/api/stream/telemetry` | SignalR hub |

**SignalR hub**

| Direction | Method / event | Purpose |
|-----------|----------------|---------|
| Client → server | `WatchFleet()` | Join fleet group (used by Expo app) |
| Client → server | `WatchVehicle(vehicleId)` | Join vehicle group (single-vehicle clients) |
| Client → server | `UnwatchFleet()` / `UnwatchVehicle(vehicleId)` | Leave groups |
| Server → client | `ReceiveTelemetryUpdate` | Live telemetry payload (camelCase) |
| Server → client | `ReceivePlaybookInstructions` | Anomaly playbook response |

The server broadcasts to both vehicle and fleet groups; the mobile app calls `WatchFleet()` only to avoid duplicate events. See [backend docs](https://bogdanvladevski.github.io/OmniOps/#mobile) for the full client bootstrap flow.

Default fleet vehicles: `Truck-001`, `Truck-002`, `Truck-003` (configurable via `FLEET_VEHICLE_IDS` / `EXPO_PUBLIC_FLEET_VEHICLES`).

---

## Stack

| Layer | Technology |
|-------|------------|
| API | ASP.NET Core 9 Minimal APIs |
| Application | MediatR (CQRS) |
| Domain | `OmniOps.Core` |
| Infrastructure | EF Core, Kafka, Redis, SignalR |
| Messaging | Apache Kafka (KRaft) |
| Database | PostgreSQL 16 |
| Cache | Redis 7 |
| Mobile | React Native + Expo 54 |
| Containers | Docker Compose |

---

## Repository layout

```text
OmniOps/
├── backend/
│   ├── OmniOps.Api/              Endpoints, middleware, host
│   │   └── Configuration/        Environment, auth, health, observability
│   ├── OmniOps.Application/      Handlers, DTOs, behaviours
│   ├── OmniOps.Core/             Entities, interfaces, events
│   ├── OmniOps.Infrastructure/ Data, Kafka workers, Redis, hubs
│   └── OmniOps.sln
├── frontend/             Expo mobile app
│   ├── components/       FleetMap, FleetDashboard, AlertsFeed
│   ├── contexts/         FleetContext (SignalR + REST snapshot)
│   └── utils/            fleetConfig, layout
├── docs/                 GitHub Pages pipeline deep-dive
├── infra/docker-compose.yml      Postgres, Redis, Kafka
├── scripts/                      Dev utilities (e.g. dotnet-install.sh)
├── .env.example                  Backend environment template
└── DEV-SETUP.md                  Setup guide
```

**Dependency flow**

```text
OmniOps.Api
 ├── OmniOps.Application → OmniOps.Core
 └── OmniOps.Infrastructure → OmniOps.Application, OmniOps.Core
```

`OmniOps.Core` has no external package dependencies.

---

## Configuration

Copy `.env.example` to `.env` at the **repository root** for the backend and Docker.

| Variable | Description |
|----------|-------------|
| `DB_CONNECTION_STRING` | Npgsql connection string (`Host=…;Port=…;Database=…;Username=…;Password=…`) |
| `REDIS_CONNECTION_STRING` | Redis connection string |
| `KAFKA_BOOTSTRAP_SERVERS` | Kafka broker (`127.0.0.1:9092`) |
| `KAFKA_MAIN_TOPIC` | Ingestion topic (`fleet-telemetry`) |
| `KAFKA_EVENTS_TOPIC` | Outbox events topic (`fleet-telemetry-events`) |
| `KAFKA_DLQ_TOPIC` | Dead-letter topic (`fleet-telemetry-dlq`) |
| `ALLOWED_CORS_ORIGINS` | Comma-separated origins (production) |

For the mobile app, copy `frontend/.env.example` to `frontend/.env`:

```text
EXPO_PUBLIC_API_URL=http://localhost:5031
EXPO_PUBLIC_FLEET_VEHICLES=Truck-001,Truck-002,Truck-003
EXPO_PUBLIC_API_TOKEN=
```

Set `EXPO_PUBLIC_API_TOKEN` when `JWT_REQUIRE_AUTHENTICATION=true` (Development token: `POST /api/auth/token` with `{"scopes":["vehicle:read"]}`).

Expo does **not** read the root `.env`. On a physical device, use your machine's LAN IP (e.g. `http://192.168.1.173:5031`).

---

## Quick start

**Prerequisites:** Docker Desktop, .NET 9 SDK, Node.js LTS

```powershell
# Environment
copy .env.example .env
copy frontend\.env.example frontend\.env

# Infrastructure
docker compose -f infra/docker-compose.yml up -d

# Backend
cd backend
dotnet run --project OmniOps.Api

# Frontend (new terminal)
cd frontend
npm install
npx expo start

# Generate telemetry (simulate all demo trucks)
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-001?packets=20"
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-002?packets=20"
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-003?packets=20"
```

API listens on `http://localhost:5031`. See [DEV-SETUP.md](DEV-SETUP.md) for troubleshooting.

---

## Testing

The backend includes two test projects under `backend/`:

| Project | Type | Focus |
|---------|------|--------|
| `OmniOps.Application.Tests` | Unit (xUnit + NSubstitute) | MediatR handlers — dedup, persistence, anomaly, cache/query |
| `OmniOps.Infrastructure.Tests` | Integration (Testcontainers) | Outbox interceptor, Redis idempotency, payload parser, Kafka wiring |
| `OmniOps.Api.Tests` | Unit (xUnit) | JWT token service, scope authorization handler |

**Prerequisites:** Docker Desktop must be running (Testcontainers starts ephemeral Postgres, Redis, and Kafka).

```powershell
cd backend
dotnet test
```

Parser-only and handler unit tests run without external services. Integration tests require Docker Desktop to be running.

---

## Docker services

| Service | Image | Host port |
|---------|-------|-----------|
| PostgreSQL | `postgres:16-alpine` | `5433` |
| Redis | `redis:7-alpine` | `6379` |
| Kafka | `apache/kafka:latest` | `9092` |

Kafka uses **KRaft** (no Zookeeper).

---

## Observability

| Component | Port / endpoint | Purpose |
|-----------|-----------------|--------|
| Serilog | Console output | Structured logs with request logging |
| Prometheus scrape | `GET http://localhost:5031/metrics` | HTTP + runtime + custom telemetry counters |
| Prometheus UI | `http://localhost:9090` | Metrics store (observability compose) |
| Grafana | `http://localhost:3000` | Dashboards (`admin` / `admin` by default) |

Start the observability stack (API must be running on the host so Prometheus can scrape `host.docker.internal:5031`):

```powershell
docker compose -f infra/docker-compose.observability.yml up -d
```

Custom metrics (meter `OmniOps.Telemetry`):

| Metric | Description |
|--------|-------------|
| `telemetry.processed` | Packets persisted and broadcast |
| `telemetry.duplicate_skipped` | Redis dedup skips |
| `telemetry.anomaly_detected` | Anomaly playbook triggers |
| `kafka.dlq_routed` | Messages sent to DLQ |
| `simulate.packets_published` | Simulate endpoint Kafka publishes |

Set `PROMETHEUS_METRICS_ENABLED=false` to disable the `/metrics` endpoint. Set `SERILOG_MINIMUM_LEVEL=Debug` for verbose logs.

---

## Security

- Secrets live in `.env` files (gitignored)
- **JWT bearer authentication** — scope-based policies (`vehicle:read`, `vehicle:simulate`); configurable via `JWT_REQUIRE_AUTHENTICATION`
- SignalR hub secured when auth is enabled; token via `accessTokenFactory` or `?access_token=` query string
- Simulate endpoint rate-limited per client IP (default 10 req / 60 s)
- Development-only `POST /api/auth/token` for issuing test tokens
- Development: permissive CORS, HTTP only (no HTTPS redirect), auth disabled by default
- Production: `JWT_REQUIRE_AUTHENTICATION=true`, strong `JWT_SECRET`, strict CORS via `ALLOWED_CORS_ORIGINS`, HTTPS enforced

---

## Roadmap

**Done:** OpenTelemetry, Serilog, Prometheus/Grafana observability stack, transactional outbox, DLQ, Redis dedup, anomaly detection, simulated playbook orchestration, unit/integration test suite, GitHub Actions CI, JWT auth + hub authorization, simulate rate limiting, FluentValidation, Polly Kafka resilience, health checks, Expo fleet dashboard (map + KPIs + alerts)

**Planned:** Dedicated worker host, Kubernetes, geofencing, route replay, real LangGraph/Semantic Kernel integration

---

## License

Portfolio project demonstrating event-driven architecture, real-time streaming, and production-grade .NET backend patterns.
