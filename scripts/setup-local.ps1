#Requires -Version 5.1
<#
.SYNOPSIS
  One-click local OmniOps stack: Docker Compose + health verification.
#>
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [int]$HealthTimeoutSec = 180
)

$ErrorActionPreference = "Stop"
$composeDir = Join-Path $RepoRoot "infra"
$apiUrl = "http://localhost:5031"

Write-Host ""
Write-Host "OmniOps local setup" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot"
Write-Host ""

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is not installed or not on PATH."
}

Push-Location $composeDir
try {
    Write-Host "Starting Docker Compose services..." -ForegroundColor Yellow
    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

function Wait-Healthy {
    param([string]$Uri, [string]$Label)
    $deadline = (Get-Date).AddSeconds($HealthTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) {
                Write-Host "  OK $Label" -ForegroundColor Green
                return $true
            }
        }
        catch {
            Write-Host "  waiting for $Label..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 3
        }
    }
    throw "Timed out waiting for $Label at $Uri"
}

Write-Host ""
Write-Host "Waiting for API health..." -ForegroundColor Yellow
Wait-Healthy -Uri "$apiUrl/health/live" -Label "liveness"
Wait-Healthy -Uri "$apiUrl/health/ready" -Label "readiness"

Write-Host ""
Write-Host "Checking demo status..." -ForegroundColor Yellow
try {
    $demo = Invoke-RestMethod -Uri "$apiUrl/api/v1/demo/status" -Method GET
    Write-Host "  Demo org: $($demo.organizationName) ($($demo.vehicleCount) vehicles)" -ForegroundColor Green
}
catch {
    Write-Host "  Demo status check failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
Write-Host "  API:      $apiUrl"
Write-Host "  Scalar:   $apiUrl/scalar/v1 (Development)"
Write-Host "  Frontend: cd frontend && npm start"
Write-Host "  E2E:      .\scripts\e2e-smoke.ps1"
Write-Host ""
