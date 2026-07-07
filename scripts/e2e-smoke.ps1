#Requires -Version 5.1
param(
    [string]$ApiBaseUrl = $(if ($env:API_BASE_URL) { $env:API_BASE_URL } else { "http://localhost:5031" }),
    [string]$VehicleId = "Truck-001",
    [string]$FleetId = "f1000000-0000-0000-0000-000000000001"
)

$ErrorActionPreference = "Stop"
$passed = 0
$failed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [int[]]$ExpectedStatus = @(200)
    )

    $uri = "$ApiBaseUrl$Path"
    Write-Host "-> $Name ($Method $Path)" -ForegroundColor Cyan
    try {
        $params = @{
            Uri             = $uri
            Method          = $Method
            UseBasicParsing = $true
        }
        if ($null -ne $Body) {
            $params.ContentType = "application/json"
            $params.Body = ($Body | ConvertTo-Json -Compress)
        }
        $response = Invoke-WebRequest @params
        if ($ExpectedStatus -notcontains $response.StatusCode) {
            throw "Expected $($ExpectedStatus -join '|') but got $($response.StatusCode)"
        }
        Write-Host "  OK $($response.StatusCode)" -ForegroundColor Green
        $script:passed++
        return $response.Content
    }
    catch {
        Write-Host "  FAIL $($_.Exception.Message)" -ForegroundColor Red
        $script:failed++
        return $null
    }
}

Write-Host ""
Write-Host "OmniOps E2E Smoke - $ApiBaseUrl"
Write-Host ""

Test-Endpoint -Name "Liveness" -Path "/health/live"
Test-Endpoint -Name "Readiness" -Path "/health/ready"
Test-Endpoint -Name "List fleets" -Path "/api/fleets"
Test-Endpoint -Name "Fleet statistics" -Path "/api/fleets/$FleetId/statistics"
Test-Endpoint -Name "Fleet vehicles" -Path "/api/fleets/$FleetId/vehicles"
Test-Endpoint -Name "Geofences" -Path "/api/geofences?fleetId=$FleetId"
Test-Endpoint -Name "Fleet telemetry snapshot" -Path "/api/telemetry/fleet"
Test-Endpoint -Name "Simulate telemetry" -Method POST -Path ("/api/test/simulate/$VehicleId" + "?packets=5")
Start-Sleep -Seconds 2
Test-Endpoint -Name "Incidents" -Path "/api/incidents?fleetId=$FleetId"

$to = (Get-Date).ToUniversalTime().ToString("o")
$from = (Get-Date).ToUniversalTime().AddHours(-6).ToString("o")
Test-Endpoint -Name "Fleet analytics" -Path ("/api/analytics/fleet/$FleetId" + "?fromUtc=$from&toUtc=$to")
Test-Endpoint -Name "Operational analytics" -Path ("/api/analytics/operational/$FleetId" + "?fromUtc=$from&toUtc=$to")
Test-Endpoint -Name "Vehicle health" -Path "/api/predictions/vehicles/$VehicleId/health"
Test-Endpoint -Name "Vehicle maintenance" -Path "/api/predictions/vehicles/$VehicleId/maintenance"
Test-Endpoint -Name "Copilot" -Method POST -Path "/api/copilot/ask" -Body @{
    question = "What should I monitor for cold-chain compliance?"
    fleetId  = $FleetId
}
Test-Endpoint -Name "Tenant workspaces" -Path "/api/v1/tenant/organizations/01000000-0000-0000-0000-000000000001/workspaces"
Test-Endpoint -Name "Admin audit logs" -Path "/api/v1/admin/audit-logs"
Test-Endpoint -Name "Admin API keys" -Path "/api/v1/admin/api-keys"
Test-Endpoint -Name "Notifications" -Path "/api/v1/notifications?limit=10"
Test-Endpoint -Name "Mobile sync" -Path "/api/v1/mobile/sync"
Test-Endpoint -Name "Demo status" -Path "/api/v1/demo/status"
Test-Endpoint -Name "Demo bootstrap" -Method POST -Path "/api/v1/demo/bootstrap" -Body @{ packetsPerVehicle = 3 }
Test-Endpoint -Name "Dev JWT" -Method POST -Path "/api/auth/token" -Body @{
    subject = "smoke-user"
    scopes  = @("vehicle:read", "vehicle:simulate", "fleet:admin", "platform:admin")
}

Write-Host ""
$color = if ($failed -eq 0) { "Green" } else { "Red" }
Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $color
Write-Host ""
if ($failed -gt 0) { exit 1 }
