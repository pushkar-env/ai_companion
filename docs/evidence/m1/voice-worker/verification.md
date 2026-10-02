# Local worker delivery and fake transport — 2026-09-27

`npm run check` passed: TypeScript, generated-client/Unicode consumer, 41 existing
session assertions and 22 new worker/transport assertions. Node 24.12.0; TypeScript
5.9.3; no new package. See checks.txt and services/voice-agent/README.md.
Preservation passed: 272 Unity input hashes unchanged, plus 36 original asset/package
hashes, metadata/GUID and baseline secret-pattern/ignore checks from check-repository.py.
No Editor tools or layout changes were used.

Verified: pending reads retain work; failed cancellation retries without blocking flush;
interrupted epochs cannot enqueue; old delayed flushes preserve newer frames; background
clears/stops transport; lost acknowledgement retries without duplicate fake settlement;
worker polling expires an orphan; failed stop holds settlement; exhaustion stops retries;
explicit recovery completes pending work; bounded frame queue; overlapping pumps rejected;
hung adapter receives abort and times out, allowing following flush to run.

Worker recreation retains coordinator and fake-sink state. It is not an OS/process crash
test, durable outbox or exactly-once network guarantee. Retry counters reset on recreation.
Fake frames contain sequence numbers only. No audio, microphone, Unity transport, provider
or real usage charge. Client immediate-silence, physical latency, native lifecycle, durable
coordination and remote cancellation/reconciliation remain unverified. No Android rebuild
needed or executed; this code is outside Unity and is not connected to the APK.

Next independent work: authenticated/durable session coordination design and wiring a
local transport boundary to the client. Live SDK/provider testing still requires the
existing Q-006 selection/region/terms/budget gate; physical tests remain owner-deferred.
