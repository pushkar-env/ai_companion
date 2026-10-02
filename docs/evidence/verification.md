# M0 verification record — 2026-09-26 IST

Configuration: local/mock-v1; synthetic input only. Windows host; Unity 6000.5.9f1;
.NET SDK 10.0.301; Python 3.14.2; Node 24.12.0; TypeScript 5.9.3.
No commit SHA exists because the repository was initialized without committing user files.
Use source-snapshot.json for exact source hashes of the cache-free reproduction snapshot.

## Passed

- `dotnet run --project tests/contract/Companion.Checks.csproj`: seven scenario groups;
  deterministic Unicode completion, loading cancellation and late delivery, partial/early
  failure with bounded retry, duplicate/gap/stale events, invalid offsets/future schemas,
  input/concurrency validation, and safe deterministic mock services.
- `python tools/build-contracts.py --check`: generated C#/TypeScript drift.
- `.venv/Scripts/python tools/check-schemas.py`: definitions, explicit UUID/time formats,
  strict writes, negative fixtures, 47 actual generated events, Unicode offset continuity,
  terminal consistency, wire budgets, supplemental voice/viseme/internal payload fixtures,
  and OpenAPI reference/operation checks.
- `npm run check`: strict TypeScript compile and generated client/Unicode consumer.
- `dotnet build services/api/Companion.MockApi.csproj --nologo`: zero warnings/errors.
- `tools/check-repository.py`: 36 original file hashes preserved, metadata presence,
  unique GUIDs, basic secret-pattern and ignore checks. Original URP/InputSystem/package
  versions and SampleScene/build-list retained. No comprehensive secret/license audit claimed.
- Live Unity MCP: compiled/imported scene and final generated client; 11 Play Mode assertions
  recorded in unity-smoke.txt; screenshot m0-complete.png visually inspected. Latest Console
  error query: zero. Runtime checks used public UI actions, not physical keyboard/touch automation.
- Source-only export to ignored artifacts/source-check-m0: 192 files with no Library,
  .env, npm cache or build output. Bootstrap plus domain/schema/TS/preservation checks passed.

## Failed then corrected

Generated integer constants were initially strings; generator fixed. RFC3339 format
support was initially absent; explicit pinned dependency added and invalid date rejected.
Strict TypeScript found missing mock transport argument types; corrected and passed.
Portrait sizing and Unity background progress were corrected before successful Play Mode run.
A reflection inspection command initially matched MonoBehaviour.SendMessage ambiguously;
the inspection used the exact signature afterward (no application failure).

## Blocked

The earlier API implementation passed HTTP admission, duplicate action/message, conflict,
SSE, replay, canonical state, cancellation and production-startup rejection. The later
binary built with expanded generated contracts was rejected at process start by Windows
Application Control: FileLoadException 0x800711C7. Current HTTP tests are **blocked**, not
passed. No attempt was made to disable or bypass the host policy or run another copied API
to evade it. Request normal host/IT approval and rerun the suite.

## Unexecuted / future gates

Fresh Unity Library/package reimport on a separate clean installation; hosted CI; IL2CPP
Android/iOS, native audio, keyboard/safe area, Hindi typography/localization, screen readers,
signing/store tests, real providers, comprehensive security/dependency/license checks,
durability/ownership and production load. These need the explicit milestones and owners
listed in STATUS.md and requirements/26_DEFINITION_OF_DONE.md.
