# Durable replay and cancellation evidence

Date: 2026-10-05. Windows local synthetic harness; PostgreSQL 18.1, .NET 10.

Passed:

- `dotnet build services/account-api/Companion.AccountApi.csproj --no-restore`: zero warnings/errors.
- `python tools/check-database.py --api`: 53 database/worker groups and 38 actual HTTP checks.
- Scoped `git diff --check`: no whitespace errors (Git emitted line-ending notices).

Final scratch run: `artifacts/database-tests/run-m0p1j7he`; cluster stopped, temporary
API credential fixture removed. Sanitized full results: [checks.txt](checks.txt).

New evidence covers reconstructed migration cursor ordering, owner-isolated replay,
negative/future cursor and page limits, canonical Unicode text, stale-version and
cross-owner cancel denial, rejecting client usage claims, cancellation with an existing
worker lease, exact cancel retry, denial of late reply, retained quota hold, bounded
page resume, empty tail, trusted cancellation settlement without duplicate event,
completion-before-cancellation conflict, rolled-back cursor allocation, API restart and
status-consumer acknowledgement, and immediate database-stop/restart cursor durability.
Existing concurrency, role/RLS, idempotency, quota and worker regression checks also pass.

Scope: M2 persistent conversation/replay/isolation foundation. This is bounded JSON replay,
not live SSE or persisted provider token deltas. Synthetic fixed worker output only.
No production identity/provider/retention decisions are inferred. No real-user data.
Cancellation fences publication; it does not prove a provider stopped or spent zero units.
Expired cancelled leases need trusted reconciliation; no automatic refund is implemented.

Unexecuted: Unity/account-history integration, live SSE reconnect, external provider
cancellation, Android/device checks. No Unity files or Editor state changed in this slice.
Blocked: real-user persistence/external identity/provider activation pending existing
owner/privacy/vendor decisions. Next safe work: bounded live SSE over these persisted
cursors, with reconnect and authorization-expiry tests, before client integration.
