# Changelog

All notable changes to OmniOps are documented in this file.

## [0.6.0] — 2026-07-03

### Added

- GitHub Actions CI workflow for backend restore, Release build, and full test execution on pushes and pull requests to `main`
- Test result artifact upload for easier CI failure inspection

## [0.5.0] — 2026-07-01

### Added

- `GET /api/telemetry/fleet` — latest cached telemetry and KPI summary for configured fleet vehicles
- SignalR `WatchFleet()` / `UnwatchFleet()` fleet group; telemetry and playbook events broadcast to fleet + vehicle groups
- Expo mobile app: bottom-tab navigation (Map, Fleet, Alerts)
- Multi-vehicle live map with vehicle chips and status-colored markers
- Fleet dashboard tab with KPI cards (active count, warnings, average fuel/temp)
- Alerts tab subscribing to `ReceivePlaybookInstructions` anomaly playbook stream
- `FLEET_VEHICLE_IDS` (backend) and `EXPO_PUBLIC_FLEET_VEHICLES` (frontend) configuration

### Changed

- Mobile app uses shared `FleetContext` SignalR connection instead of per-screen connections
- Default fleet demo vehicles: `Truck-001`, `Truck-002`, `Truck-003`
- Renamed `omniops-frontend/` to `frontend/`; mobile layout uses safe-area insets for phone displays

## [0.4.0] — 2026-07-01

### Added

- Serilog structured logging with request logging, environment enrichment, and `SERILOG_MINIMUM_LEVEL` configuration
- Prometheus metrics via OpenTelemetry (`GET /metrics`) with custom counters for telemetry processing, DLQ routing, anomalies, and simulate publishes
- `infra/docker-compose.observability.yml` — Prometheus (`:9090`) and Grafana (`:3000`) with a pre-provisioned OmniOps dashboard

### Changed

- OpenTelemetry console trace export enabled only in Development
- Runtime, ASP.NET Core, and HTTP client metrics exported when `PROMETHEUS_METRICS_ENABLED=true`

## [0.3.0] — 2026-07-01

### Added

- FluentValidation for MediatR commands and queries
- Polly retry (jittered exponential backoff) and circuit breaker on Kafka produce paths
- `IKafkaMessageProducer` abstraction with `ResilientKafkaMessageProducer`
- Health endpoints: `GET /health/live`, `GET /health/ready` (Postgres, Redis, Kafka)
- Validator unit tests in `OmniOps.Application.Tests`

### Changed

- `ValidationBehaviour` uses FluentValidation instead of inline validation hooks
- `GlobalExceptionMiddleware` returns HTTP 400 with field errors for validation failures
- Outbox worker, Kafka consumer DLQ routing, and simulate endpoint use the resilient Kafka producer
- Invalid telemetry that parses as JSON but fails validation is routed to the DLQ

## [0.2.0] — 2026-07-01

### Added

- JWT bearer authentication with scope policies (`vehicle:read`, `vehicle:simulate`)
- Development-only `POST /api/auth/token` for issuing test tokens
- Simulate endpoint rate limiting (default 10 requests / 60 s per client IP)
- `OmniOps.Api.Tests` for JWT token service and scope authorization
- Optional `EXPO_PUBLIC_API_TOKEN` for SignalR when auth is enabled

### Changed

- Telemetry, simulate, and SignalR hub routes require authorization when `JWT_REQUIRE_AUTHENTICATION=true`
- API pipeline includes authentication, authorization, and rate limiting middleware

### Environment

- `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES`, `JWT_REQUIRE_AUTHENTICATION`
- `SIMULATE_RATE_LIMIT_PERMIT_LIMIT`, `SIMULATE_RATE_LIMIT_WINDOW_SECONDS`
- `EXPO_PUBLIC_API_TOKEN` (frontend)

## [0.1.0] — 2026-07-01

### Added

- `OmniOps.Application` layer with MediatR CQRS handlers and pipeline behaviours
- `OmniOps.Application.Tests` (xUnit + NSubstitute) and `OmniOps.Infrastructure.Tests` (Testcontainers)
- Transactional outbox interceptor, Redis deduplication, Kafka DLQ routing
- `TelemetryPayloadParser` for testable Kafka payload deserialization
- OpenTelemetry tracing, environment-driven configuration (`.env.example`)
- Playbook orchestration, SignalR broadcast service, telemetry repository

### Changed

- Backend reorganized into Clean Architecture (Api → Application → Core, Infrastructure)
- Kafka outbox publishes to `fleet-telemetry-events` (separate from ingestion topic)
- Docker Compose aligned with root `.env` (KRaft Kafka, Postgres on port 5433)
