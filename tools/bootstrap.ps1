$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function CheckExit { if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" } }
if (-not (Test-Path .venv)) { python -m venv .venv; CheckExit }
& .venv/Scripts/python -m pip install -r tools/requirements.txt; CheckExit
& .venv/Scripts/python tools/build-contracts.py --check; CheckExit
dotnet restore services/api/Companion.MockApi.csproj; CheckExit
dotnet restore tests/contract/Companion.Checks.csproj; CheckExit
npm ci --ignore-scripts --no-audit --no-fund; CheckExit
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
Write-Host 'Ready. Open apps/unity with Unity 6000.5.9f1, then Companion > Open M0 Mock Scene.'
Write-Host 'Optional mock API: dotnet run --project services/api/Companion.MockApi.csproj'
