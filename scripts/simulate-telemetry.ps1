#Requires -Version 5.1
param(
    [string]$ApiBaseUrl = "http://localhost:5031",
    [string]$VehicleId = "Truck-001",
    [int]$Packets = 10
)

$uri = "$ApiBaseUrl/api/test/simulate/$VehicleId" + "?packets=$Packets"
Write-Host "POST $uri"
$response = Invoke-RestMethod -Uri $uri -Method POST
$response | ConvertTo-Json
