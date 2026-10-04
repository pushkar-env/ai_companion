# Local durable conversation foundation

This is an M2 backend foundation using synthetic account data. It is not exposed through
the prototype or a production API. Guest trials remain session-only; account history and
purchases require the account boundary approved in Q-011. No real user data belongs here.

## Run the checks

Prerequisites: Python 3 and installed PostgreSQL 18 server/client binaries. Verified on
Windows with PostgreSQL 18.1 at `C:/Program Files/PostgreSQL/18/bin`. No package download,
Docker, cloud account or shared server is required. From repository root:

```powershell
python tools/check-database.py
```

For another approved installation, set `PG_BIN` to its bin directory in your shell first.
The tool checks version/tools, creates a unique cluster beneath ignored
`artifacts/database-tests/`, chooses an unused loopback port and generates a random local
password. Authentication uses SCRAM. It starts only that cluster, applies the migration,
runs synthetic SQL, concurrent sessions and abrupt shutdown/recovery, then stops the
cluster in cleanup and removes the temporary password file. Test clusters remain stopped
for inspection; nothing deletes or migrates an existing database. Evidence is written to
`docs/evidence/m2/database/checks.txt`. Do not upload ignored cluster files or logs.

## Schema and application boundary

`services/api/Database/001_conversations.sql` is a transactional, apply-once bootstrap
migration. It requires a precreated non-owner `companion_runtime` role; the harness uses
NOLOGIN with no superuser/BYPASSRLS privileges. A separate migration owner creates tables.
Do not rerun this bootstrap against an existing application schema or claim production
migration/rollback readiness. No destructive down migration is supplied.

For future API integration, begin a transaction, derive the verified account UUID from
server-side identity, use parameterized `set_config('companion.user_id', actor, true)`,
and call `companion.accept_text(conversation, key, client_message, text)`. Commit the
transaction before returning accepted. Do not use session-level SET, accept an owner UUID
from the client, expose arbitrary SQL or let clients choose the runtime database role.
The test role is an application identity boundary, not authentication itself.

Admission serializes per owner/conversation, returns the persisted turn ID on matching
retries and rejects conflicting keys/client IDs or a second active turn. Message, turn,
sequence/version, retry key and pending outbox reference share a transaction. Runtime
may update only user version (needed for row locking), not reactivate deleted accounts.
RLS and composite foreign keys prevent cross-owner reads/links/writes. Outbox contains
IDs and event metadata; workers must fetch authorized content later.

## Not implemented / not verified

No provider/safety execution, API-served history or SSE event history,
external worker/broker delivery, retention/deletion operation, OIDC, guest linking,
encryption/KMS deployment, load test or production restore procedure. Clean scratch-cluster
crash recovery is not a backup/tombstone restore test. PostgreSQL 18.1 is the installed
local test version, not a claim of an approved/patched production deployment version.
The existing .NET API's historical Application Control block still requires a fresh check
or approved host remediation; these database tests do not bypass that restriction.

## Terminal state and outbox extension

Migration `002_terminal_outbox.sql` applies after 001. The same harness runs both in
sequence and now reports 23 check groups. `finish_text(turn, expected_version, state,
text)` accepts completed/cancelled/failed; only completed accepts non-empty assistant
text. Exact terminal retries return the same ID, while stale versions and conflicting
terminal results reject. Pass approved/canonical text from the future server policy
pipeline; never expose this database function directly to a client. Speech heard-state
still requires its separate playback-receipt flow.

`process_next_status_event()` processes at most one pending event for the transaction's
actor. It returns an event ID or NULL. Call in a short transaction with a bounded outer
worker loop. Row locks/SKIP LOCKED permit concurrent consumers. Dedupe, monotonic status
projection and published_at acknowledgement commit together; retry after rollback is
safe. This specific consumer is database-local turn-status-v1. published_at does not
mean a network message was delivered. External broker/provider delivery and background
worker identity/lifecycle are not yet implemented. No real-user retention changes occur.

## Quota extension

Migration 003 adds `reserve_usage(budget, request_key, units, expires_at)` and
`settle_usage(reservation, actual_units)`. The harness now runs 34 check groups across
all three migrations. Caps/periods/units require explicit server configuration; only
test SQL seeds synthetic amounts. No production price or free allowance is selected.

Compose reserve and accept in one transaction; settle from trusted actual usage, not
client claims. A terminal reservation retry is not permission for fresh provider work.
Settlement is idempotent and releases unused units; zero releases all. Usage above the
hold is rejected: funding increments/reconciliation must exist before provider admission.
Expired holds remain funded until reconciled. No expiry worker/provider reconciliation
or API integration exists yet. Runtime cannot raise caps or edit/delete ledger entries.
