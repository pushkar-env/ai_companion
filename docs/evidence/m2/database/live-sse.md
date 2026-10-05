# Local durable SSE evidence

Date: 2026-10-05. Windows, PostgreSQL 18.1, .NET 10; synthetic disposable accounts only.

Passed:

- `dotnet build services/account-api/Companion.AccountApi.csproj --no-restore`: zero warnings/errors.
- `python tools/check-database.py --api`: 53 database/worker groups + 47 HTTP checks (100 total).
- SSE tests use actual HTTP sockets: missing/other-owner identity denied; malformed/future/
  conflicting cursors rejected; ordered Unicode replay; Last-Event-ID resumes exclusively;
  idle connection permits concurrent cancellation; newly committed terminal event arrives
  without reconnect; four-stream cap leaves ordinary API available; disconnect frees slot;
  an already open connection closes at token expiry and further requests receive 401.
- Existing crash recovery, cursor, canonical history, quota, cancellation fencing, worker
  leases, owner isolation and idempotency regression checks remain green.

Scratch run: artifacts/database-tests/run-ds8310h1. Test cluster stopped and ephemeral
credential fixture removed. Full sanitized results: [checks.txt](checks.txt).

M2 contribution: durable event replay plus live owner-scoped transport. Events describe
admission/terminal outcomes, not provider token deltas. Reconnect does not dispatch work.
No Unity or Editor changes; portrait remains preserved. No external credentials/services.

Unexecuted: Unity reconnect UI, production proxy/slow-consumer/load benchmarks, mobile
network handover, explicit 30-second maximum-duration test, owner-status change during
an open stream, injected midstream database failure. Token-expiry closure was exercised.
Blocked: real-user identity/provider/storage activation pending existing owner policies.
Next safe task: synthetic account/history client integration with cursor persistence,
EOF recovery and duplicate-event handling; maintain the visible local-only disclosure.
