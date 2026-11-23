# Changelog

All notable changes to OmniOps are documented in this file.

## [Unreleased]

### Added

- **Today** (the home screen, formerly Live): the map, plus three numbers — trucks moving, things that need you, deliveries to invoice — and a "Needs you" list in plain words with one next step each: a load outside its temperature range or a check overdue, a truck with no signal, a truck waiting past its free time (and a heads-up 15 minutes before), a reported problem, a late job, a job due soon that hasn't started, a job without a driver. Then the trucks on the road (late and silent ones first), jobs not started, money (ready to invoice, overdue), and today's deliveries
- New companies get a setup checklist on Today (trucks, drivers, first job, a driver's phone, invoice settings) that ticks itself off
- Drivers' phone numbers (Fleet): "Call Stefan" on Today and on the map's job card. Fleet now lists drivers, with the phone editable in place
- Job timeline: created, arrived at pickup, left, arrived at the customer, delivered, invoiced — with times
- Search (Ctrl K) finds jobs by customer, order number, place, truck or driver, and invoices by number or customer
- The audit trail reads in plain words ("Invited someone to the team — ana@… as dispatcher")
- Waiting time (detention) at pickup and delivery, recorded by the driver's phone: the delivery wait starts when GPS reaches the customer's address (or the driver taps "I've arrived") and ends at hand-over; the pickup wait runs from the driver's arrival tap until the trip starts. A tap far from the address is shown but not treated as proof
- Companies set free waiting time and an hourly rate (Invoices → Invoice settings); waiting past the free time is charged per started 15 minutes and offered as a ready-priced line when invoicing the job, with the arrival and release times in the description. Without a rate, waiting is recorded but not charged
- The office is alerted when a truck's free waiting time runs out; the receiver's tracking page shows when the truck arrived and from when waiting is charged; the invoice page lists the times at each stop
- Jobs can store the pickup's location (looked up from the "From" address) so a driver's arrival there can be checked
- `POST /api/v1/driver/jobs/{id}/arrived` (`stop`: `pickup` | `delivery`)
- Driver app works without signal: starting a trip, photos, temperature readings, signatures, problem reports and confirming delivery are saved on the phone and sent in order when there is a connection; the phone shows what is still waiting and never claims something was sent before it was. Times recorded are when the driver did it, not when it arrived
- Receiver signs on the driver's phone; the signature (with the signer's name) is proof of delivery on its own and appears on the job and the customer's invoice page
- Problem reports: damage, shortage, wrong goods or refusal, with a photo and a short description, at loading or delivery; the office is alerted, and they appear on the job and the invoice page
- Photo check: a blurry or very dark loading note or delivery photo is flagged on the phone ("Take it again" / "Use it anyway") before it is saved
- Driver app in 9 languages: English, Macedonian, Albanian, Serbian/Croatian/Bosnian, Bulgarian, Romanian, German, Turkish and Polish — taken from the phone, changeable in the app, independent of the office
- Team: owners invite dispatchers or other owners by email (Admin → Team); the invitation link works once, for 7 days, and is emailed when SMTP is set up — otherwise the owner copies it into WhatsApp, Viber or email. Owners change roles, remove people (signed out at once) and withdraw invitations; a company always keeps at least one owner
- Passwords: "Forgot password?" sends a one-hour, single-use reset link when email is set up; otherwise an owner creates one for a teammate from the Team card. Resetting or changing a password signs the account out on every other device
- Account settings: change your name and password, sign out on all devices; owners rename the company
- Company accounts: sign up (company, first fleet, owner), sign in with email and password, sign out; owners and office staff see only their own company
- Drivers connect their phone with a one-time 8-character code from the office (Admin → Connect a driver's phone); a driver sees only their own jobs and nothing of the office
- Fleet: add trucks from the app
- Invoicing: a delivered job becomes an invoice in a minute (customer details remembered, line pre-filled with route and order); numbers are sequential per company and unique even when issued at the same moment; issued invoices are snapshots that later edits do not change
- Customer invoice page at `/i/{token}`: printable/save as PDF, with how to pay and the delivery's proof (loading note, delivery photos, temperature log) underneath
- Sending: by email when SMTP is configured, otherwise copy/share the link; payment reminders 3 days before due, on the day, and 7 and 14 days overdue, once each, stopping when paid (listed for the office when the customer has no email)
- Invoices screen: what is waiting for payment, overdue and paid, delivered jobs still to invoice, and invoice settings (company details, bank account, VAT, terms, numbering)
- Delivery paperwork: the driver photographs the signed loading note (CMR) at pickup; delivery photos and loading notes are grouped on the job
- Temperature log for refrigerated jobs: the office sets a range (chilled 2–8 °C, frozen −25 to −18 °C or custom); the driver logs the fridge display reading with a photo at loading, every 2 hours and at delivery; a reading outside the range alerts the office at once; a refrigerated job cannot be closed without a reading from the last 30 minutes
- `POST /api/v1/driver/jobs/{id}/documents?kind=PickupDocument|DeliveryPhoto|TemperaturePhoto[&temperatureCelsius=]`
- Live is now a map of today's deliveries: trucks on the way with arrival times, jobs not started, deliveries made; a truck whose phone stops reporting is flagged "No signal" instead of showing an out-of-date arrival time
- "Load demo day" (onboarding and Admin) creates two trucks on the road with a GPS trail and one planned delivery; running it again moves the trucks to where they would be by now
- Delivery jobs: the office creates a job (truck, driver, customer, destination) on the new **Jobs** board and follows it live
- Driver mode: a phone shows the driver's jobs, shares GPS while a trip runs (queued offline, resent safely), and closes the job with a photo and the recipient's name
- Customer tracking page at `/t/{token}`: live position and arrival estimate while on the way, delivery time and recipient afterwards; no login, nothing about the driver or other jobs, expires a week after delivery
- Delivery photos stored on a Docker volume (`STORAGE_ROOT_PATH`)

### Security

- Driver uploads carry an id from the phone; a resend is stored once. Times from the phone are kept between job creation and now, so a wrong phone clock can't backdate or future-date paperwork; a delivery's temperature reading must be from the 30 minutes before hand-over
- Every fleet, vehicle, driver, incident, analytics, prediction, telemetry, audit-log and live-update path is limited to the caller's company (several still assumed a single company); truck history is stored per fleet so same-named trucks in different companies never mix
- Sign-in is required by default and the API refuses to start outside Development with it switched off; requests without a company are refused instead of becoming the demo company
- Wrong passwords lock an account for 15 minutes after 5 tries; refresh tokens are single use
- Removing a person, changing their role, resetting or changing a password and "sign out on all devices" end existing sign-ins immediately, not when their access token expires
- The old, unfinished `POST /api/v1/tenant/invitations` endpoint (its invitations could never be accepted) was removed

### Changed

- Fixed: GPS readings from before a trip started could be taken as the arrival at the customer
- Fixed: job photos in the office app were blank in the browser since sign-in became required (the browser didn't send the sign-in with images)
- Fixed: while offline, the driver app could say the location was "last sent" at a time when nothing had been sent
- The Analytics tab is no longer in the navigation (Invoices takes its place); its screen is kept in the code
- "Copy link" shows the link to copy by hand when the browser blocks the clipboard, instead of claiming it was copied
- Onboarding describes the delivery product instead of cold-chain monitoring; the demo no longer generates simulated cargo temperatures
- `POST /api/v1/demo/bootstrap` requires the job-board permission when authentication is on
- Cargo temperature and fuel level are optional on telemetry; phone GPS sends neither, and no rule treats a missing reading as 0
- Harsh braking/acceleration is only judged between readings at most 5 s apart (phone GPS every ~15–30 s made every stop look harsh)
- Business-rule conflicts return 409 with a readable message; transient failures return 503 with `Retry-After`

### Removed

- Legacy `/api/trips` endpoints (replaced by `/api/v1/jobs`)

### Changed (architecture)

- Runs as one API container plus PostgreSQL. Telemetry ingestion moved from Kafka to a bounded in-process queue with the same retry rules; deduplication, excursion tracking and the latest-telemetry cache moved from Redis to process memory (single instance)
- Cargo-temperature alerts are broadcast as `ReceiveAlert` (`TemperatureExcursion` / `TemperatureTrend`) after commit
- Excursion state is tracked per fleet and vehicle, so same-named vehicles in different fleets no longer share a timer

### Removed

- Kafka, Redis, the fleet copilot and LLM incident narratives
- Helm chart, Kubernetes notes, Prometheus/Grafana compose stack and the Kubernetes deploy workflow

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
