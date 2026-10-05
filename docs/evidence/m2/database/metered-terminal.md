# Atomic metered terminal transition - 2026-10-05

Passed: `python tools/check-database.py --api`, PostgreSQL 18.1, **42 SQL groups plus
23 actual HTTP checks**. Full current output is checks.txt. Eight added groups exercise:

- Worker-only execute privilege on the new entry (runtime/account role excluded).
- Over-reservation settlement error rolls back completed state, reply and terminal event.
- Exact retry charges once, releases unused hold and creates one canonical reply/event.
- Changed usage or reply is rejected; another owner cannot complete the turn.
- Cancellation and failure settle explicitly supplied synthetic units without assistant text.
- Two real concurrent psql sessions complete the same metered turn only once.
- Immediate PostgreSQL stop/restart and duplicate delivery preserve the reply and ledger.

Migration 005 uses SECURITY INVOKER and owner-first locking, composes finish_text with
settle_usage and requires a bound reservation. Existing RLS and version checks remain.
The initial test attempt found an ambiguous fixture variable named id; it was renamed
test_turn, then the complete suite passed. No production database was involved. The
harness starts a scratch cluster with random local credentials and stops it afterward;
credential fixtures are not committed. No provider calls, paid resources or real-user data.

Limitations: this is a database worker entry, not a running AI worker or production-auth
boundary. The companion_runtime role retains earlier trusted low-level grants, so this
change does not establish complete SQL privilege separation. The local API has no public
completion route. External worker identity, leases/dispatch, provider usage reconciliation,
persisted SSE/history and production identity/data policies are unimplemented. Unity
Alita chat remains session-only. No pricing, cancellation charging or retention policy
is inferred from synthetic accounting units.
