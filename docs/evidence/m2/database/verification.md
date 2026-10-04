# PostgreSQL conversation foundation — 2026-10-04

Command: `python tools/check-database.py`. Windows, installed PostgreSQL 18.1, unique
scratch cluster under ignored artifacts, random loopback port and SCRAM test password.
No existing server/database, Unity scene, credential or external account was modified.

**Passed: 12 integration check groups** (`checks.txt`): absent-actor denial; owner reads
and atomic admission; key conflict/active-turn rejection; cross-owner admission/link/write
denial; transaction actor reset; rollback atomicity; runtime role ownership/RLS restrictions;
deleting-owner admission/reactivation rejection; input bounds/closed conversation rejection;
rollback context reset; two concurrent duplicate sessions; restart after immediate shutdown.
Concurrent requests returned one persisted turn/event. Crash recovery retained accepted
turn IDs, unpublished outbox references and the idempotent retry result.

The initial Windows harness hung because detached server processes retained captured
output pipes. Its isolated cluster was stopped and its temporary password removed;
the harness now uses file-backed subprocess output. Subsequent runs completed and stopped
their clusters successfully. No existing PostgreSQL service was stopped or reconfigured.

Existing regression check `npm run check` passed: TypeScript compilation/generated-client
consumer, synthetic voice/session/playback contracts and 19 actual local HTTP assertions.

**Not covered:** real identity/API authorization, quota transactions, durable completed
assistant replies, SSE replay, outbox delivery/consumer dedupe, production migration,
performance, encrypted deployment, privacy retention, deletion-tombstone restore or guest
data transfer. This is a DATA-01/DATA-03/ARCH-02 subset; M2 and production readiness remain
partial. No policies were silently selected. User approved guest trial with account for
saved history/purchases; guest retention/limits/transfer details remain open.
