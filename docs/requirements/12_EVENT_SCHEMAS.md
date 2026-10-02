# Event contracts and delivery semantics

## EVENT-01 — envelope

Use application-owned versioned JSON Schema. Envelope required fields: event_id UUID, schema_version integer=1, type allowlisted string, aggregate_id UUID, seq nonnegative integer, occurred_at RFC3339, trace_id string and payload object. Server internal events also include owner_id; client-facing events omit unnecessary identifiers. Each schema defines required payload fields, sizes, enum/range checks and additionalProperties policy. Additive optional fields preserve v1; breaking changes require v2 and migration.

```json
{
  "event_id": "44444444-4444-4444-8444-444444444444",
  "schema_version": 1,
  "type": "turn.text.delta",
  "aggregate_id": "22222222-2222-4222-8222-222222222222",
  "seq": 3,
  "occurred_at": "2026-09-25T10:00:00Z",
  "trace_id": "example-trace",
  "payload": {"text": "Let's start with", "offset": 0}
}
```

## EVENT-02 — client event types

| Type | Required payload | Semantics |
|---|---|---|
| turn.accepted | user_message_id | Durable acceptance, not model completion |
| turn.text.delta | text, offset | Moderated text; offset in Unicode code points, not UTF-16 units |
| turn.expression | expression metadata from behavior spec | Optional UI cue; invalid metadata becomes neutral |
| turn.completed | assistant_message_id, text, finish_reason | Canonical full text replaces accumulated draft |
| turn.failed | code, retryable, partial_text | Terminal failure; no silent restart |
| turn.cancelled | reason, partial_text | Terminal user/system cancellation |
| voice.state | session_id, state, playback_epoch | Reliable ordered control |
| voice.transcript | utterance_id, role, text, is_final, playback_epoch | Interim text may be replaced |
| voice.playout | utterance_id, played_ms, playback_epoch | Client observation, not billing authority |
| voice.interrupted | utterance_id, heard_ms, playback_epoch | Worker reconciles with actual session state |
| avatar.visemes | utterance_id, playback_epoch, frames | Media-clock-relative frames; drop stale frames |
| entitlement.changed | snapshot_version | Refetch authorized snapshot; not a grant command |
| memory.invalidated | memory_epoch | Rebuild context and UI cache |

Viseme frames: array max 200 per event, each `{offset_ms,duration_ms,weights}`; nonnegative offsets from utterance playout start, duration 1..500 ms, weights a sparse allowlisted viseme→0..1 map. Total event size ≤32 KiB. Viseme frames may use a lossy transport; session state, cancellation and entitlement invalidation require reliable control. Audio is a media track, never base64 JSON in an event.

SSE uses `id: <event_id>`, `event: <type>`, JSON envelope in data. Store resumable text events for a proposed 10-minute replay window, enforce owner checks on reconnect, dedupe event_id and validate seq. Gap or expired replay returns a documented reset condition; client fetches canonical TurnState. Token expiry closes stream and requires refresh/reconnect. Heartbeats carry no transcript. Clients ignore unknown optional event types and report schema incompatibility rather than crash.

## EVENT-03 — internal domain events

Durable outbox types: `message.completed` (message_id, consent_epoch), `memory.candidate.created` (candidate_id), `memory.deleted` (memory_id, memory_epoch), `purchase.verified` (provider_event_id, transaction_id), `entitlement.updated` (user_id, snapshot_version), `asset.catalog.published` (catalog_version, platform), `privacy.deletion.requested` (deletion_id, user_id), `usage.finalized` (reservation_id, normalized_units), `report.created` (report_id). Do not copy full chat text into general event buses; workers read authorized data by ID.

Persist outbox in the same DB transaction as domain changes. Consumers store event_id dedupe and effects atomically. No global ordering guarantee; aggregate versions and state reconciliation handle reorder. Retry transient errors with bounded exponential backoff; dead-letter persistent failures with redacted metadata and operator replay. Deletion must invalidate queued stale jobs.

Acceptance: duplicate, reordered, missing, oversized, stale-epoch and future-version fixtures do not double-apply, leak or crash; SSE replay reconstructs exact Unicode text; interruption discards late visemes/audio; event schemas and generated types are checked for drift in CI.
