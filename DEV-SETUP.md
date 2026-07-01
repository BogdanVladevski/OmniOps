# Developer Setup

Step-by-step guide to run OmniOps locally on Windows, macOS, or Linux.

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | PostgreSQL, Redis, Kafka |
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ | Backend API |
| [Node.js](https://nodejs.org/) | LTS | Expo mobile app |

Recommended editor extensions: **C# Dev Kit**, **Docker**.

---

## 1. Clone and configure environment

From the repository root:

```powershell
copy .env.example .env
copy omniops-frontend\.env.example omniops-frontend\.env
```

### Root `.env` (backend + Docker)

Used by the .NET API and `docker-compose`. Key values:

```text
DB_CONNECTION_STRING=Host=127.0.0.1;Port=5433;Database=OmniOps;Username=postgres;Password=123
REDIS_CONNECTION_STRING=127.0.0.1:6379,abortConnect=false
KAFKA_BOOTSTRAP_SERVERS=127.0.0.1:9092
```

Use **semicolon-separated** Npgsql connection strings. Do not use comma syntax or single quotes — DotNetEnv will fail to parse them.

### Frontend `.env` (Expo only)

```text
EXPO_PUBLIC_API_URL=http://localhost:5031
```

| Target | `EXPO_PUBLIC_API_URL` |
|--------|------------------------|
| iOS Simulator / Android Emulator on same machine | `http://localhost:5031` |
| Physical phone on same Wi-Fi | `http://<your-lan-ip>:5031` |

Find your LAN IP: `ipconfig` (Windows) or `ifconfig` (macOS/Linux).

---

## 2. Start infrastructure

Ensure Docker Desktop is running, then:

```powershell
docker compose -f infra/docker-compose.yml up -d
```

Verify containers:

```powershell
docker ps --filter "name=omniops"
```

Expected: `omniops-db`, `omniops-cache`, `omniops-kafka`.

### Postgres credential changes

If you change `POSTGRES_USER`, `POSTGRES_PASSWORD`, or `POSTGRES_DB` after Postgres has already initialized, reset the volume:

```powershell
docker compose -f infra/docker-compose.yml down -v
docker compose -f infra/docker-compose.yml up -d
```

---

## 3. Start the backend

```powershell
cd backend
dotnet run --project OmniOps.Api
```

On success you should see:

```text
Database migrations applied successfully
Now listening on: http://0.0.0.0:5031
```

The API auto-applies EF Core migrations on startup.

---

## 4. Start the frontend

In a **new terminal**:

```powershell
cd omniops-frontend
npm install
npx expo start
```

After changing `omniops-frontend/.env`, restart with a clean cache:

```powershell
npx expo start -c
```

---

## 5. Generate telemetry

With the API running:

```powershell
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-001?packets=10"
```

When `JWT_REQUIRE_AUTHENTICATION=true`, include a bearer token with the `vehicle:simulate` scope:

```powershell
$body = @{ scopes = @("vehicle:simulate") } | ConvertTo-Json
$token = (Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/auth/token" -Body $body -ContentType "application/json").accessToken
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-001?packets=10" -Headers @{ Authorization = "Bearer $token" }
```

The simulate endpoint is rate-limited (default: 10 requests per 60 seconds per client IP). Exceeding the limit returns HTTP 429.

On macOS/Linux (real curl):

```bash
curl -X POST "http://localhost:5031/api/test/simulate/Truck-001?packets=10"
```

Open the Expo app — the map should show **Live Map Connected** and the marker should move as packets are processed.

---

## 6. Authentication (optional local / required production)

By default, `JWT_REQUIRE_AUTHENTICATION=false` in `.env.example` so local development works without tokens.

### Enable JWT locally

In the root `.env`:

```text
JWT_SECRET=your-random-string-at-least-32-characters-long
JWT_REQUIRE_AUTHENTICATION=true
```

Restart the API, then issue a development token (Development environment only):

```powershell
$body = @{ scopes = @("vehicle:read", "vehicle:simulate") } | ConvertTo-Json
$response = Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/auth/token" -Body $body -ContentType "application/json"
$response.accessToken
```

### Mobile app with auth enabled

Add the token to `omniops-frontend/.env`:

```text
EXPO_PUBLIC_API_TOKEN=<paste accessToken here>
```

Restart Expo with `npx expo start -c`.

### Scopes

| Scope | Grants access to |
|-------|------------------|
| `vehicle:read` | `GET /api/telemetry/{id}`, SignalR hub `/api/stream/telemetry` |
| `vehicle:simulate` | `POST /api/test/simulate/{id}` |

SignalR passes the JWT via `accessTokenFactory` (Bearer header). WebSocket negotiate also accepts `?access_token=` query parameter.

### Production

Set `JWT_REQUIRE_AUTHENTICATION=true`, use a strong `JWT_SECRET`, and **do not** expose `/api/auth/token` (it is Development-only).

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `dockerDesktopLinuxEngine` pipe not found | Start Docker Desktop and wait until the engine is running |
| `Superpower.ParseException` on API startup | Fix `.env` format — use semicolons in `DB_CONNECTION_STRING`, no single quotes |
| `password authentication failed for user "postgres"` | Postgres volume has stale credentials — run `docker compose … down -v` and recreate |
| `Cannot resolve 'undefined/api/stream/telemetry'` | Create `omniops-frontend/.env` with `EXPO_PUBLIC_API_URL`, then `npx expo start -c` |
| SignalR `Network request failed` on phone | Set `EXPO_PUBLIC_API_URL` to your LAN IP, not `localhost` |
| SignalR fails on simulator | Restart API after pulling latest code (HTTPS redirect is disabled in Development) |
| `401 Unauthorized` on API or SignalR | Set `JWT_REQUIRE_AUTHENTICATION=false` for open local dev, or obtain a token via `/api/auth/token` and set `EXPO_PUBLIC_API_TOKEN` |
| `429 Too Many Requests` on simulate | Rate limit exceeded — wait for the window to reset or adjust `SIMULATE_RATE_LIMIT_*` in `.env` |
| PowerShell `curl -X POST` fails | Use `Invoke-RestMethod -Method POST -Uri "…"` or `curl.exe -X POST "…"` |
| Kafka connection errors at API startup | Normal if Kafka is still booting — the consumer retries automatically |
| `/health/ready` returns Unhealthy | Ensure Docker containers are running; check Postgres, Redis, and Kafka ports |

---

## Health checks

| Endpoint | Purpose |
|----------|---------|
| `GET /health/live` | Liveness — returns 200 if the process is running |
| `GET /health/ready` | Readiness — verifies Postgres, Redis, and Kafka connectivity |

```powershell
Invoke-RestMethod -Uri "http://localhost:5031/health/ready"
```

---

## Observability (optional)

With the API running on the host:

```powershell
docker compose -f infra/docker-compose.observability.yml up -d
```

| Service | URL | Credentials |
|---------|-----|-------------|
| Prometheus | http://localhost:9090 | — |
| Grafana | http://localhost:3000 | `admin` / `admin` |
| API metrics | http://localhost:5031/metrics | — |

Grafana auto-loads the **OmniOps Overview** dashboard from `infra/observability/grafana/dashboards/`.

Generate traffic so metrics appear:

```powershell
Invoke-RestMethod -Method POST -Uri "http://localhost:5031/api/test/simulate/Truck-001?packets=10"
```

---

## Port reference

| Service | Port |
|---------|------|
| API (HTTP) | `5031` |
| PostgreSQL | `5433` |
| Redis | `6379` |
| Kafka | `9092` |
| Prometheus | `9090` |
| Grafana | `3000` |
| Expo dev server | `19006` (default) |
