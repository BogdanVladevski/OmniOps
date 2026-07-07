# OmniOps Testing Guide

This document covers automated tests and **manual simulatory verification** for Phase 17 (QA) and Phase 18 (frontend).

---

## Automated tests

### Backend (unit + integration + load)

```powershell
# From repo root — requires Docker (Testcontainers)
dotnet restore backend/OmniOps.sln
dotnet build backend/OmniOps.sln -c Release
dotnet test backend/OmniOps.sln -c Release --logger "console;verbosity=normal"
```

Integration tests spin up ephemeral **PostgreSQL**, **Redis**, and **Kafka** containers and exercise REST APIs without background workers.

### Frontend (mobile unit tests)

```powershell
cd frontend
npm install
npm test
```

### E2E smoke (running API)

With Docker Compose or `dotnet run` API listening on port **5031**:

```powershell
# PowerShell
.\scripts\e2e-smoke.ps1

# Optional custom URL
.\scripts\e2e-smoke.ps1 -ApiBaseUrl http://localhost:5031
```

```bash
# Bash / Git Bash / WSL
chmod +x scripts/e2e-smoke.sh
./scripts/e2e-smoke.sh http://localhost:5031
```

---

## Manual full-app simulation

### 1. Start infrastructure + API

```powershell
# Terminal 1 — Postgres, Redis, Kafka, API container
docker compose -f infra/docker-compose.yml up -d --build

# Verify readiness (PowerShell — use this in Cursor terminal on Windows)
Invoke-RestMethod http://localhost:5031/health/ready
```

**Alternative (API on host):**

```powershell
docker compose -f infra/docker-compose.yml up -d postgres redis kafka
cd backend/OmniOps.Api
dotnet run
```

> **Note:** Windows PowerShell does not support `curl -X POST` the same way as bash. Use `Invoke-RestMethod` or the helper scripts below instead of `curl`.

### 2. Run E2E smoke script

```powershell
.\scripts\e2e-smoke.ps1
```

All checks should pass (green ✓).

### 3. Start mobile / web frontend

```powershell
# Terminal 2
cd frontend
# Ensure frontend/.env exists:
# EXPO_PUBLIC_API_URL=http://localhost:5031
# EXPO_PUBLIC_FLEET_VEHICLES=Truck-001,Truck-002,Truck-003
npm install
npx expo start
```

Press `w` for web, or scan QR for device/emulator.

### 4. Simulate live telemetry

```powershell
# Option A — helper script (recommended on Windows)
.\scripts\simulate-telemetry.ps1 -VehicleId Truck-001 -Packets 10

# Option B — PowerShell one-liner
Invoke-RestMethod -Uri "http://localhost:5031/api/test/simulate/Truck-001?packets=10" -Method POST
```

Wait ~5–10 seconds for the consumer to process.

### 5. UI walkthrough checklist

| Tab | What to verify |
|-----|----------------|
| **Map** | Vehicle markers move/update; geofence overlays; vehicle chips select target |
| **Fleet** | Executive KPIs (active, warnings, trips, incidents); tap vehicle → **Vehicle Detail** modal (health, maintenance, trends) |
| **Manage** | Fleet stats; vehicle search/filter; create driver; Copilot answer |
| **Analytics** | Fleet KPIs, operations, driver safety, speed/temp charts, predictions, heatmap |
| **Alerts** | Playbook/incident alerts after temperature excursion simulate |
| **Admin** | Audit logs load; API keys list; **Generate Key** works; Refresh |

### 6. Trigger cold-chain incident (optional)

```powershell
.\scripts\simulate-telemetry.ps1 -Packets 15
Invoke-RestMethod "http://localhost:5031/api/incidents?fleetId=f1000000-0000-0000-0000-000000000001"
```

Check **Alerts** tab and SignalR live updates.

### 7. Scalar API docs (development)

Open [http://localhost:5031/scalar/v1](http://localhost:5031/scalar/v1) to explore all endpoints interactively.

### 8. Dark mode

Long-press the **Admin** tab (or use the hidden dev toggle top-right) to switch light/dark theme.

---

## Performance expectations

Integration load tests assert:

- `/health/live` — 40 concurrent requests complete in &lt; 10s
- `/api/fleets` — median latency &lt; 1s over 20 samples

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| API can't reach Postgres in Docker | Ensure `DB_HOST=postgres` in compose; host `dotnet run` uses `127.0.0.1:5433` |
| Admin tab shows auth error | Set `JWT_REQUIRE_AUTHENTICATION=false` in `.env` for dev |
| No telemetry on map | Run simulate endpoint; confirm Kafka consumer logs in API |
| Frontend "Missing EXPO_PUBLIC_API_URL" | Create `frontend/.env` with API URL |
| Expo Go: Network request failed | Phone cannot use `localhost` — app auto-rewrites to LAN IP; ensure phone and PC share Wi‑Fi, API is on `:5031`, Windows Firewall allows inbound |
| Integration tests fail | Docker Desktop must be running |
