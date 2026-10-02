$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function CheckExit { if ($LASTEXITCODE -ne 0) { throw "Check failed with exit code $LASTEXITCODE" } }
& .venv/Scripts/python tools/build-contracts.py --check; CheckExit
dotnet run --project tests/contract/Companion.Checks.csproj; CheckExit
& .venv/Scripts/python tools/check-schemas.py; CheckExit
npm run check; CheckExit
dotnet build services/api/Companion.MockApi.csproj --nologo; CheckExit
& .venv/Scripts/python tools/check-repository.py; CheckExit
& .venv/Scripts/python tests/e2e/check_api.py
if ($LASTEXITCODE -eq 77) { Write-Warning 'BLOCKED: API execution by host policy. Other checks passed; overall verification remains partial.'; exit 77 }
CheckExit
Write-Host 'PASS local automated suite. Unity Play Mode/device checks are separate; see docs/STATUS.md.'
