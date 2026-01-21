# -------------------------------
# Fixon Vertical Slice Test (PowerShell)
# -------------------------------
# This script is aligned to the current API routes in `src/Fixon.Api/Program.cs`.
# It targets BOOTSTRAP mode (Bootstrap.Enabled=true) where endpoints are:
#   POST /facts
#   POST /claims
#   POST /claims/{id}/submit
#   GET  /penalties/{claimId}

$ErrorActionPreference = "Stop"

$BaseUrl = "http://localhost:5198"

function Invoke-JsonPost($uri, $bodyObj) {
    $json = $bodyObj | ConvertTo-Json -Depth 10
    return Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $json
}

Write-Host "=== Fixon Vertical Slice (BOOTSTRAP mode) ==="
Write-Host "BaseUrl: $BaseUrl`n"

# 0) Basic health check (helps diagnose wrong BaseUrl/port quickly)
Write-Host "=== Health ==="
try {
    $health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/ready"
    Write-Host "Health ready: OK`n"
} catch {
    Write-Host "Health ready: FAILED"
    throw
}

# 1) Import a fact
Write-Host "=== 1) Import Fact ==="
$externalId = "vs01-" + ([Guid]::NewGuid().ToString("n"))
$occurredAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$factBody = @{
    externalId    = $externalId
    occurredAtUtc = $occurredAtUtc
    # IMPORTANT: in bootstrap mode CreateClaim requires fact.value > ThresholdValue (default ThresholdValue=10)
    value         = 11
}

$factResp = Invoke-JsonPost "$BaseUrl/facts" $factBody
$factId = $factResp.factId
Write-Host "Fact imported. factId=$factId isDuplicate=$($factResp.isDuplicate)`n"

# 2) Create claim for that fact
Write-Host "=== 2) Create Claim ==="
$claimResp = Invoke-JsonPost "$BaseUrl/claims" @{ factId = $factId }
$claimId = $claimResp.claimId
Write-Host "Claim created. claimId=$claimId`n"

# 3) Submit claim
Write-Host "=== 3) Submit Claim ==="
$submitResp = Invoke-RestMethod -Method Post -Uri "$BaseUrl/claims/$claimId/submit"
Write-Host "Claim submitted. status=$($submitResp.status)`n"

# 4) Get calculated penalty (if available)
Write-Host "=== 4) Get Penalty ==="
try {
    $penalty = Invoke-RestMethod -Method Get -Uri "$BaseUrl/penalties/$claimId"
    Write-Host "Penalty:"
    $penalty | ConvertTo-Json -Depth 10
} catch {
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode.value__ -eq 404) {
        Write-Host "Penalty not found yet (404)."
        Write-Host "This can be expected if penalty calculation is async or not triggered for this claim."
    } else {
        throw
    }
}

Write-Host "`n=== Vertical Slice Test Completed ==="
