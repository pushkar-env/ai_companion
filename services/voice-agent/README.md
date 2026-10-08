# Local voice-session simulator

`local-http.mjs` adds a separate **synthetic loopback test endpoint**. Run
`npm run check:voice:http` to start it on an ephemeral 127.0.0.1 port, exercise 19
real HTTP checks and shut it down automatically. `npm run check` includes these checks.
Fixtures register delivery metadata server-side; only terminal playback reports cross
HTTP. Random per-account tokens stay in memory, expire after five minutes, and have a
200-request fixture budget. Exact Host validation, browser-Origin rejection, a 4 KiB
body cap and sanitized errors constrain this local harness. These are test limits,
not approved product quotas or production identity. No tokens are printed or saved.
Unity is not connected to this endpoint yet. No live provider, media or database is
used. This does not replace the .NET API or resolve its Windows Application Control block.

The suite currently runs **114 voice assertions**. `playback-receipts.ts` wraps the Unity
report bridge with account/session isolation and a trusted delivery registration.
Register the fixed delivered-sample bound and timing before accepting a terminal report;
reports must match format/epoch and cannot exceed that bound. Conflicting duplicates
fail; exact duplicates return the first finalized receipt. Registering a newer generation
rejects unfinished older reports. This local slice assumes a fixed delivery snapshot,
not an incrementally growing streaming ledger. Registration and identity are supplied by
trusted test code, not verified from network clients. No public HTTP endpoint exists.
The store is volatile/bounded, drops draft alignment on finalization and retains receipts
until discarded with the store; no user-data retention policy is established.

Run `npm run check:voice` from the repository root with Node 24.12.0. `npm run check`
also type-checks the module and runs the existing generated-client contract checks.

`session-coordinator.ts` implements a deterministic, in-memory lifecycle for synthetic
sessions: one active session per account/companion pair, idempotent connect/end, bounded
heartbeat leases, absolute duration cap, generation epochs and ordered media-frame
admission. Interrupt/completion invalidate previous epochs. Background, logout, policy,
quota and expiry produce cancellation, playback-flush, stop and settlement actions.

The caller supplies a monotonic millisecond clock and explicit lease/duration/capacity
limits. The 30-second lease and 60-second duration in tests are fixture values, not
approved product quotas. Call `tick()` independently of client traffic in a future
worker; no scheduler is started by this module. `drainActions()` exposes simulated
commands, not actual provider cancellation. Elapsed session time is diagnostic data,
not a provider usage meter or charge. Terminal records remain for idempotency until the
local capacity is reached; then admission fails rather than losing replay information.

All identities are synthetic trusted inputs. This module is not exposed as an HTTP API,
does not authenticate clients, mint room tokens, store transcripts, or connect to Unity.
It has no network, microphone, provider SDK, money, raw audio or external account access.
No production exactly-once guarantee: action delivery and records are volatile. A real
worker needs authenticated scopes, durable coordination/outbox/usage, a watchdog,
provider cancellation/reconciliation and native transport/playout wiring.

LiveKit remains the required first native transport reference. AI/voice providers remain
unselected under Q-006. Unity's portrait mock and CC diagnostic scenes stay unchanged.

## Local worker and fake transport

`local-worker.ts` consumes `pendingActions()` and acknowledges successful delivery by
stable action ID. It retains failed work, bounds attempts and per-attempt time, passes
an abort signal on timeout, and exposes exhausted actions as blocked. Retry is driven
by explicit `pump()` calls; there is no background scheduler. `retryBlocked()` is an
explicit local recovery action. Use a single worker per coordinator. The original
destructive `drainActions()` is retained for isolated fixtures; do not mix it with worker
delivery. Recreation of the worker preserves queued work only if the same coordinator
and sink objects remain alive; retry counters restart. This is not process-crash recovery.

The fake sink deduplicates effects by action ID and models a bounded queue of sequence
numbers, epoch flushing, cancellation, terminal stop and diagnostic elapsed-time
settlement. It supports failure before application and acknowledgement loss afterward.
Old delayed flushes cannot erase newer frames. Settlement waits for stop acknowledgement;
a failed cancellation does not hold up separate flush/stop actions. No waveform is played.

The worker portion runs 41 coordinator and 22 worker/transport assertions. A
production adapter must honor abort or otherwise reconcile late effects, provide durable
idempotency and enforce immediate client-side silence. A timeout is not proof that a
remote provider stopped. Neither these tests nor fake settlement establish billing or
250 ms physical interruption acceptance.

## Heard-response tracking

`heard-response.ts` accepts trusted, explicitly timed transcript segments for one
session/epoch. Feed actual playout position through `reportPlayed()`, then finalize
before invalidating/flushing that epoch. Interruption keeps only fully played segments;
completion requires the full audio duration. `context()` returns the retained assistant
text and interruption flag, or no entry when nothing aligned was heard. It never
estimates a prefix from text length or queued/generated audio. Timing segments must not
split grapheme clusters (including Hindi combining marks and emoji).

This is volatile bookkeeping, not an authenticated client-report endpoint or durable
conversation history. Missing alignment omits uncertain text. The fixture bounds
(five-minute response, 2,000 segments, 16,000 UTF-16 units) limit memory, not product
quotas. The terminal tracker releases its own unheard draft; this is not a claim of
provider deletion. Native playout clocks, provider timing conversion and transcript
quality remain unimplemented. The full offline suite runs 114 assertions: 41 lifecycle,
22 worker, 22 heard-response, 10 Unity bridge and 19 scoped receipt checks.

## Talking-character progressive endpoint

`talking-character.mjs` is the separate real local Windows adapter. `/turn-stream` now
uses Ollama streaming JSON: ordered `delta` frames contain decoded text, `text` supplies
the matching canonical reply/emotion, `audio` frames have an independent sequence, and
`done.sequence` gives the clip count. Audio can precede canonical text. At most 600 text
characters and three speech jobs; existing 90-second deadline/abort behavior retained.
Restart an existing local service after source changes. Never print the session token.
Run `node tests/e2e/check-streamed-reply.mjs` for deterministic decoding/concurrency, and
`node tests/e2e/check-sentence-stream.mjs` against the running local adapter for real
model/PCM/cancellation checks. This is not a mobile or cloud voice transport.
