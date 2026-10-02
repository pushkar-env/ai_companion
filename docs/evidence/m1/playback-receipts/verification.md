# Scoped playback receipts — 2026-09-29

Passed `npm run check`: TypeScript/generated client and 114 voice checks, including
19 receipt assertions. Tests cover wrong account/session, altered rate/duration,
over-delivery progress, stale epoch, invalid sample counts, copied alignment, idempotent
retry, conflicting retry, stale unfinished generation, old finalized replay, duplicate
registration and capacity failures preserving existing entries. See checks.txt.

Passed `node tests/e2e/check-unity-playback.mjs`: saved Editor report from the previous
turn consumed through the receipt store, retaining 341 ms observed progress and only
the fully played synthetic label. Exact retry returns the same receipt. This reuses
existing Editor evidence; no new Editor run or physical test was performed. The delivery
registration derives from that fixture, not an independently observed real network send.

No Unity/Editor changes, new dependencies, account access, provider calls or spending.
Live authentication, trusted server delivery measurement, streaming registration updates,
durable receipt/idempotency storage and device tests remain unexecuted. M1 stays partial.
The receipt store is local code for a future server boundary, not a deployed endpoint.

Next integration step: an authenticated transport must supply the trusted identity and
delivery registration; never construct those values from an untrusted playback report.
