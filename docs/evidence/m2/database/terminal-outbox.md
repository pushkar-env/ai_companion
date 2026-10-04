# Terminal turns and outbox — 2026-10-04

`python tools/check-database.py`: **23 check groups passed**, PostgreSQL 18.1, Windows,
isolated loopback synthetic clusters. `checks.txt` contains the fresh combined results.
Migration 001 tests still pass before migration 002 extends the schema.

New evidence covers one canonical sequenced assistant reply and terminal event; exact
Hindi/emoji persistence; stale versions and conflicting terminal retry rejection;
cancel/failure slot release without fake assistant text; actual cross-owner turn-ID
rejection; consumer fault after dedupe insertion rolling back all effects; bounded
owner-scoped processing; duplicate redelivery and non-regressing status projection;
two concurrent sessions racing completion versus cancellation (one winner); concurrent
consumers; abrupt shutdown/recovery retaining terminal states, messages and dedupe.
Runtime roles remain non-owner/no-BYPASSRLS and all calls use transaction-local actors.

The consumer fault is an intentionally installed test trigger removed after the check.
Two concurrent terminal requests may choose either winner; the test verifies state,
assistant-message presence and exactly one terminal event match that winner. SQL test
input and subprocess I/O explicitly use UTF-8. No microphone or real account data used.

`git diff --check` passed. Unity scene, orientation, asset metadata and Editor layout
were untouched. No production migration or deployment was performed.

Limitations: the consumer produces a database-local status projection, not a broker or
provider side effect. No long-running worker, production identity, quota, moderation,
SSE history, guest transfer, encryption deployment or deletion/retention is verified.
Unicode storage does not establish Hindi UI/STT/TTS quality. M1/M2 remain partial.
