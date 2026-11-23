# Development guide

## Prerequisites

- .NET SDK 10 (the exact version is pinned in `global.json`)
- Node.js 22 or later
- Docker (for PostgreSQL and the integration tests), usable without `sudo`
- The Aspire CLI, which runs the local environment: `dotnet tool install -g aspire.cli`. It must be on your `PATH`
  (`~/.dotnet/tools`); otherwise the AppHost build stops with `ASPIRE009`.

```bash
dotnet tool restore      # EF Core tools (dotnet-tools.json)
(cd app && npm ci)
```

## Run everything

```bash
dotnet run --project src/OmniOps.AppHost
```

Aspire starts PostgreSQL (with a persistent volume), the API (<http://localhost:5031>) and the app
(<http://localhost:8081>), and prints a link to its dashboard: logs, traces and metrics for every resource. The JWT
signing key for development is generated once and stored in the AppHost's user secrets.

If the dashboard shows `postgres` stuck in *Starting* and the API waiting, Docker isn't reachable from your terminal:
your account was added to the `docker` group after you logged in. Log out and back in once, or start with
`sg docker -c "dotnet run --project src/OmniOps.AppHost"`. If Docker reports a port "already in use" after Docker itself
was restarted, a stale port forwarder is left behind: `sudo snap restart docker` (or `sudo systemctl restart docker`)
clears it.

Useful addresses:

| What | Where |
| --- | --- |
| App | <http://localhost:8081> |
| API reference (Scalar) | <http://localhost:5031/docs> |
| OpenAPI document | <http://localhost:5031/openapi/v1.json> |
| Health | <http://localhost:5031/health>, `/alive` |

On a development server the sign-in screen offers **Try the demo company**. In the demo company,
*Settings → Load or refresh demo day* creates a day of deliveries with trucks on the road.

## Run pieces separately

API against your own PostgreSQL:

```bash
export ConnectionStrings__omniops="Host=localhost;Database=omniops;Username=omniops;Password=…"
export Jwt__SigningKey="a-development-key-of-at-least-32-characters"
dotnet run --project src/OmniOps.Api
```

App:

```bash
cd app
EXPO_PUBLIC_API_URL=http://localhost:5031 npm run web
```

## Test on a phone

Phone mode makes the API and the app's dev server reachable from your Wi-Fi, so the real app runs on your phone in
[Expo Go](https://expo.dev/go) with GPS and the camera:

```bash
OMNIOPS_PHONE=1 dotnet run --project src/OmniOps.AppHost
```

1. Put the phone on the same Wi-Fi as this computer and install Expo Go (it must support the project's Expo SDK).
2. In the Aspire dashboard, the `app` resource shows an **Open in Expo Go** link (`exp://<this computer>:8081`). In
   Expo Go, choose *Enter URL manually* and type it, or scan the QR code in the `app` resource's console log.
3. The office still works in this computer's browser at <http://localhost:8081>. On the phone, sign in to the demo
   company, or connect as a driver: *Fleet → Connect phone* in the office, then "I'm a driver" on the phone.

The computer's address is found automatically; set `OMNIOPS_LAN_HOST` to override it. If the phone can't connect, a
firewall is blocking ports 5031 and 8081 (on Ubuntu: `sudo ufw allow 5031/tcp && sudo ufw allow 8081/tcp`). Tracking,
portal and invitation links use the computer's network address in this mode, so they open on the phone too.

GPS is shared while the app is open. Background tracking needs a development build (planned).

## Tests

| Suite | Command | Notes |
| --- | --- | --- |
| Unit | `dotnet test --project tests/OmniOps.UnitTests` | Domain rules and conventions; no Docker |
| Integration | `dotnet test --project tests/OmniOps.IntegrationTests` | Real API and PostgreSQL via Testcontainers; each test creates its own company |
| App | `cd app && npm test` | Jest |
| App checks | `npm run lint && npm run typecheck && npm run format:check` | |

If Docker isn't running, the integration tests fail to start. That's the environment, not a regression.

## Database migrations

```bash
dotnet ef migrations add <Name> --project src/OmniOps.Infrastructure --startup-project src/OmniOps.Api \
  --output-dir Persistence/Migrations
```

The API applies pending migrations at start-up (`Database:MigrateOnStartup`, on by default).

## The API contract

Building `OmniOps.Api` writes `docs/api/omniops-api.json`. The app's client types are generated from it:

```bash
cd app && npm run api:types
```

CI regenerates both and fails if the committed files differ, so the contract and the app can't drift apart.
