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
The .NET API's historical Application Control block did not reproduce on 2026-10-04:
fresh builds and HTTP checks passed without security-policy changes.

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
exists yet. Runtime cannot raise caps or edit/delete ledger entries. Local admission API
integration is now available below; no production provider operations are authorized.

## Synthetic account API integration

```powershell
dotnet restore services/account-api/Companion.AccountApi.csproj --locked-mode
dotnet build services/account-api/Companion.AccountApi.csproj --no-restore
python tools/check-database.py --api
```

This applies migration 004, creates a non-owner database login, seeds synthetic accounts
and caps, generates temporary random bearer tokens, and starts a loopback API on a random
port. The harness tests it, stops it and removes its credential fixture. It runs 34 SQL
groups plus 23 real HTTP checks; no manual credentials needed. The account API is distinct
from M0 and never changes Unity retention. APP_ENV must be local and
SYNTHETIC_ACCOUNTS_ONLY must be true; configuration comes from an ignored fixture file.

Development endpoints: POST /local/v1/conversations/{id}/admissions with Idempotency-Key
and client_message_id/text; GET /local/v1/turns/{id}. Identity comes from the temporary
token, not a JSON owner field/header. Accepted work can be handled by the synthetic worker described below; there is no live
SSE endpoint yet. These routes are not the production OpenAPI contract.
Migration 004 binds each turn to one reservation; quota failure rolls back the entire
acceptance. Production identity, rate limiting, public API completeness and terminal
settlement/worker integration remain pending.

## Atomic metered completion

Migration 005 follows 004. The same disposable test command now executes **42 SQL groups
and 23 HTTP checks**. Its migration owner needs role-creation privileges to create the
NOLOGIN companion_worker role; never apply these migrations to a production database
without the normal reviewed migration/authorization process.

A trusted worker transaction sets its authenticated owner context and calls:
`companion.finish_metered_text(turn_id, expected_version, terminal_state, reply_text, actual_units)`.
Supported terminal states: completed with bounded text; cancelled/failed with NULL text.
Actual units must be trusted observed usage, bounded by the turn's reservation; zero
releases the entire hold. A repeated call must preserve original expected version,
state, text and usage. Different text/state/units conflicts, and no partial output commits.
Use this combined function for metered worker work instead of independently calling
finish_text and settle_usage. Earlier primitives and table grants are trusted internal
SQL capabilities; runtime DB credentials must never reach clients.

The account API cannot invoke the new entry and exposes no completion HTTP route.
Worker login/authentication, dispatch, retries/reconciliation of uncertain provider
usage, account-deletion reconciliation and persisted SSE/history remain future work.
Synthetic failed/cancelled unit examples do not select production charging rules.

## Local worker lease extension

Migration 006 adds the synthetic worker lifecycle. The standard command now passes
51 database/worker groups and 23 HTTP checks. See services/worker/README.md for the bounded
one-pass process and its local-only configuration. The harness executes the worker with
a dedicated non-owner login; it requires no manually supplied credentials.

Worker calls: claim_local_turn(worker_uuid, seconds) returns turn/token/version;
renew_local_turn(turn, token, seconds) returns false for lost or terminal work;
finish_local_turn(turn, token, expected_version, state, text, actual_units) commits terminal
output and settlement with the winning receipt. Token expiry/replacement fences stale
completion. Account API credentials cannot read worker tokens or invoke these functions.
These trusted SQL functions do not authenticate an external worker on their own.

Do not enable paid provider retries from this synthetic lease logic: unknown provider
outcomes and usage must be reconciled first. Current process makes no provider calls,
prints no conversation content, completes at most one turn, and exits rather than polls.

## Durable replay and cancellation extension

Migration 007 follows 006. Current validation:

```powershell
dotnet build services/account-api/Companion.AccountApi.csproj --no-restore
python tools/check-database.py --api
```

Requires the already configured .NET SDK, restored pinned NuGet packages and local
PostgreSQL tools (PG_BIN if not in the default installation). This uses disposable
synthetic accounts and no external service. Expected: 53 database/worker + 38 HTTP checks.

Development routes using the same ephemeral bearer fixture:

- GET `/local/v1/conversations/{id}/events?after=0&limit=50`
- GET `/local/v1/conversations/{id}/messages?after=0&limit=50`
- POST `/local/v1/turns/{id}/cancel` with `{"expected_version":1}`

Pages contain items, next_cursor, has_more, conversation_id and synthetic mode. Resume
using next_cursor on the same route/conversation; after is exclusive. Maximum page size
100. Unknown/other-owner conversation returns 404; negative/future cursor or invalid
page size returns 400. Cursor at current tail returns an empty page. Responses are
no-store. Event IDs remain stable across API/database restart and status consumption.
Message status may change after it was paged; use terminal events to refresh known turns.
Do not treat an old message cursor as a subscription to message status updates.

Cancel returns 200 on first/exact retry, 409 for stale/conflicting terminal requests,
404 for other-owner/absent turn. It does not release quota. A trusted worker with a
live lease may acknowledge cancelled with observed usage through finish_local_turn;
expired or uncertain work requires separate trusted reconciliation. Never infer zero
provider usage from timeout/cancellation. No client-supplied usage field is accepted.
These routes are local-only foundations, not the production OpenAPI or live SSE contract.

## Live local SSE transport

GET `/local/v1/conversations/{id}/stream?after=0` with the existing Authorization bearer
header. Resume with `Last-Event-ID: <last processed sequence>` on the same conversation.
If both header and after are supplied, they must agree. An unauthorized conversation
returns 404 before streaming; malformed/future cursors return 400. Four occupied stream
slots yield 429. Clients should back off on 429/503, reauthenticate on 401, and never
retry a rejected cursor unchanged indefinitely. Do not put tokens in URLs.

Each frame has id, event and a single JSON data line. Comment heartbeats do not advance
the cursor. A stream stays open after terminal events to follow future turns, for at
most 30 seconds or until token expiry/disconnect. Reconnect with the last processed id;
the retry hint is 1000ms. An EOF alone is not a completed turn: only persisted terminal
events or canonical turn state establish completion. Reconnect after EOF; an expired
token must be renewed via the eventual identity flow (local fixtures are not production auth).
No automatic generation/admission happens on reconnect.

Database connections are not held while waiting on the client. Poll batches contain at
most 50 events; no unbounded in-memory replay queue. Local heartbeat/poll frequency is
500ms. This is for synthetic loopback testing; fair account limits, production proxy
behavior, slow-consumer/load evidence and Unity client integration remain pending.

## Synthetic client foundation

`packages/account-client/README.md` documents the .NET 10 client API and its local guards.
The existing `python tools/check-database.py --api` now builds the client checks and runs
them against the disposable account API as well as interrupted-transport socket fixtures.
The client keeps credentials, cursor and projected history in memory only. It does not
wire the Unity conversation to persistent accounts or change approved retention scope.
Current suite: 53 database/worker, 47 HTTP and 13 client checks (113 total).
