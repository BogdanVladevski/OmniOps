#!/usr/bin/env bash
# OmniOps end-to-end smoke test against a running API.
# Usage: ./scripts/e2e-smoke.sh [API_BASE_URL]
set -euo pipefail

API_BASE_URL="${1:-${API_BASE_URL:-http://localhost:5031}}"
VEHICLE_ID="${VEHICLE_ID:-Truck-001}"
FLEET_ID="${FLEET_ID:-f1000000-0000-0000-0000-000000000001}"
PASSED=0
FAILED=0

check() {
  local name="$1" method="${2:-GET}" path="$3" body="${4:-}"
  echo "→ $name ($method $path)"
  local args=(-s -o /dev/null -w "%{http_code}" -X "$method")
  if [[ -n "$body" ]]; then
    args+=(-H "Content-Type: application/json" -d "$body")
  fi
  local code
  code=$(curl "${args[@]}" "$API_BASE_URL$path" || echo "000")
  if [[ "$code" =~ ^2 ]]; then
    echo "  ✓ $code"
    PASSED=$((PASSED + 1))
  else
    echo "  ✗ HTTP $code"
    FAILED=$((FAILED + 1))
  fi
}

echo ""
echo "OmniOps E2E Smoke — $API_BASE_URL"
echo ""

check "Liveness" GET "/health/live"
check "Readiness" GET "/health/ready"
check "List fleets" GET "/api/fleets"
check "Fleet statistics" GET "/api/fleets/$FLEET_ID/statistics"
check "Fleet vehicles" GET "/api/fleets/$FLEET_ID/vehicles"
check "Geofences" GET "/api/geofences?fleetId=$FLEET_ID"
check "Fleet telemetry" GET "/api/telemetry/fleet"
check "Simulate telemetry" POST "/api/test/simulate/$VEHICLE_ID?packets=5"
sleep 2
check "Incidents" GET "/api/incidents?fleetId=$FLEET_ID"

FROM=$(date -u -d '6 hours ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v-6H +%Y-%m-%dT%H:%M:%SZ)
TO=$(date -u +%Y-%m-%dT%H:%M:%SZ)
check "Fleet analytics" GET "/api/analytics/fleet/$FLEET_ID?fromUtc=$FROM&toUtc=$TO"
check "Operational analytics" GET "/api/analytics/operational/$FLEET_ID?fromUtc=$FROM&toUtc=$TO"
check "Vehicle health" GET "/api/predictions/vehicles/$VEHICLE_ID/health"
check "Copilot" POST "/api/copilot/ask" "{\"question\":\"Fleet status?\",\"fleetId\":\"$FLEET_ID\"}"
check "Tenant workspaces" GET "/api/v1/tenant/organizations/01000000-0000-0000-0000-000000000001/workspaces"
check "Admin audit logs" GET "/api/v1/admin/audit-logs"
check "Mobile sync" GET "/api/v1/mobile/sync"
check "Dev JWT" POST "/api/auth/token" '{"subject":"smoke-user","scopes":["vehicle:read","platform:admin"]}'

echo ""
echo "Results: $PASSED passed, $FAILED failed"
echo ""
[[ "$FAILED" -eq 0 ]]
