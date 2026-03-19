$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host "Starting backend (Fixon.Api)..."
dotnet run --project ".\src\Fixon.Api\Fixon.Api.csproj"
