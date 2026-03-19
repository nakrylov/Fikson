$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$backendScript = Join-Path $root "run-backend.ps1"
$frontendScript = Join-Path $root "run-frontend.ps1"

if (-not (Test-Path $backendScript)) {
    throw "File not found: $backendScript"
}

if (-not (Test-Path $frontendScript)) {
    throw "File not found: $frontendScript"
}

Write-Host "Starting backend and frontend in separate PowerShell windows..."

Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$backendScript`""
Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$frontendScript`""

Write-Host "Done."
Write-Host "Backend:  http://localhost:5198"
Write-Host "Frontend: http://localhost:5173"
