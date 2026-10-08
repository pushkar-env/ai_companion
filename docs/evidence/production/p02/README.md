# P02 — durable conversation commands, locally verified

2026-10-08; Unity 6000.5.9f1 stopped Windows Editor, PostgreSQL 18.1,
.NET SDK 10.0.301. Final disposable run: run-miswefua.

`SyntheticConversationCommands` adds a write-side adapter alongside the existing
`SyntheticHistoryTransport` read/replay adapter. Both are explicitly synthetic and
scope one instance to one account credential and conversation. No production endpoint
or real-user persistence is enabled, and normal TalkingCompanion chat is unchanged.

## Composition contract for P03

1. Create command/history adapters only after explicit synthetic fixture selection.
   Keep the normal local AI conversation separate and visibly identify the lab mode.
2. Prepare captures immutable text, client message ID and idempotency key. Do not
   create another command while it is unresolved. Retry retains both IDs and text.
3. Admission is not completion. Reconcile GET /turns after the receipt; an idempotent
   admission receipt can say accepted even when the durable turn is already terminal.
4. History/SSE supplies canonical messages. Do not fabricate assistant text or treat
   a local command acknowledgement as an AI response. Pending UI remains separate.
5. Stop aborts local transport only. Explicit Cancel resolves unknown admission using
   the same key, reads the current version, requests cancellation if still accepted,
   then reconciles again. Completion racing cancellation wins according to server truth.
   Resolving unknown admission may admit then cancel a turn; it does not imply a refund.
6. 401/403 closes same-session command access; Stop/new draft cannot reopen it.
   Dispose both adapters on account switch. P03 must clear the prior visible account
   before binding the replacement. Never transfer pending work across account scopes.
7. Only reconciled terminal commands can be cleared. Transport errors and local abort
   leave an uncertain outcome. No automatic provider retry, refund or usage release.

Requests use a 15-second timeout, no redirects, fixed routes, immutable local credentials
and validated canonical loopback origins. Invalid admission/turn payloads fail to an
uncertain state rather than falsely claiming delivery/completion. One operation per
adapter; no background scheduler, provider execution or new database migration.

## Verification

- Account API build passed with zero warnings/errors.
- `python tools/check-database.py --api --unity-commands` passed 114 harness groups,
  including its matching run-ID gate for 16 Unity command checks.
- [editor-checks.txt](editor-checks.txt): 16 actual UnityWebRequest/API/PostgreSQL checks.
  First admission is committed through the API and its acknowledgement deliberately
  withheld from the adapter. Retrying results in one canonical Unicode turn, stable
  IDs, repeatable cancellation and no resurrection. Non-owner admission denied.
  Invalid token forces reload and cannot be bypassed with Stop or Prepare.
- [database-checks.txt](database-checks.txt): copied sanitized final harness output.
  Includes prior transactional concurrency/crash, cancellation-race, quota and RLS checks.
- Editor remained stopped; no scene/layout/Game-view/build-target edits. Console errors
  empty. Disposable fixture removed and PostgreSQL test cluster stopped by the harness.

Use the pinned local SDK directory on PATH when invoking the harness; at READY run
`Companion > Run Durable Conversation Command Checks` in the stopped Editor. No fixture
credential values should be printed. The harness creates its own synthetic conversation
and adds test budget only in its disposable database; no product allowance is chosen.

## Limits

Pending request identities are memory-only: process-death persistence is P09. Production
auth/refresh, durable AI dispatch/streaming, device execution and normal chat UI binding
remain P03/P05/P07 and later gates. The lost-receipt test models acknowledgement loss by
discarding the real receipt, not by network packet loss. A live completion/cancel race
is covered at database level; the new client test exercises accepted→cancelled.
The command adapter is Editor-only by configuration, not an Android compatibility claim.
