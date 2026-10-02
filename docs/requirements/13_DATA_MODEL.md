# PostgreSQL, pgvector and Redis data model

## DATA-01 — durable entities

Use UUID primary keys, timestamptz timestamps, explicit foreign keys, check constraints and monotonically increasing resource versions. Business uniqueness lives in the database, not only in code. All user-owned child rows carry user_id, with composite parent/owner constraints where practical to prevent cross-owner links. Sensitive text is encrypted at rest with controlled database/KMS access; apply application field encryption to especially sensitive fields if required by the approved threat model. Vector search requires searchable vectors; do not claim ordinary pgvector can operate on application-encrypted vectors.

| Table | Main fields | Required keys/indexes and lifecycle |
|---|---|---|
| users | id, display_name, locale, status, memory_epoch, deletion_requested_at | status index; soft tombstone before purge |
| identities | user_id, issuer, subject | UNIQUE(issuer,subject); validated linking |
| consent_records | user_id, purpose, policy_version, granted, recorded_at | append-only evidence; latest by purpose |
| character_definitions | id, version, schema, rig_family, policy_version | UNIQUE(id,version); immutable published revisions |
| companions | user_id, definition_id/version, name, traits, voice_profile_id, familiarity_state, version | index(user_id,id) |
| conversations | user_id, companion_id, state, latest_turn_at | composite ownership FK; index(user_id,latest_turn_at,id) |
| turns | user_id, conversation_id, state, modality, provider_request_id, config_version, reservation_id | one active generation per conversation; recovery timestamps |
| messages | user_id, conversation_id, turn_id, role, text, sequence, client_message_id, status, heard_ms | UNIQUE(conversation_id,sequence); UNIQUE(user_id,client_message_id) when present |
| conversation_summaries | user_id, conversation_id, source_range, text, memory_epoch, version | source dependencies invalidate on delete |
| memories | user_id, companion_id, text, type, confidence, status, consent_version, expires_at, deleted_at | owner/scope/status indexes |
| memory_sources | user_id, memory_id, message_id | composite ownership FKs; reverse source index |
| memory_embeddings_vN | memory_id, user_id, companion_id, model_id, dimension, embedding | fixed dimension vector(D) per table; unique memory/model version |
| catalog_items | id, item_type, rig_compatibility, slots, metadata, status | immutable release versions |
| asset_manifests | id, platform, client_schema_range, hash, object_key, bytes, status | UNIQUE(platform,hash) |
| store_product_mappings | catalog_item/entitlement_id, store, product_id, environment | UNIQUE(store,product_id,environment) |
| purchase_events | provider, environment, event_id, transaction_id, received_at, processing_state | UNIQUE(provider,environment,event_id) |
| transactions | user_id, store, environment, transaction_id, original_transaction_id, state | UNIQUE(store,environment,transaction_id) |
| entitlements | user_id, entitlement_id, source, state, expires_at, version | UNIQUE(user_id,entitlement_id,source) |
| inventory | user_id, catalog_item_id, source_transaction_id, state | enforce one effective owned grant per source/item |
| loadouts | user_id, companion_id, slot_items, version | UNIQUE(companion_id); server slot validation |
| voice_sessions | user_id, companion_id, room_id, lease_version, state, deadline, last_heartbeat | UNIQUE(room_id); active-account lease constraint |
| usage_reservations | user_id, period_id, modality, reserved_units, settled_units, state | idempotent request ref; lock budget ledger atomically |
| usage_ledger | user_id, reservation_id, units, unit_type, provider, model, estimated_cost_minor, price_version | append-only corrections; UNIQUE(settlement_key) |
| devices | user_id, installation_id, platform, encrypted_push_token, timezone, last_seen | UNIQUE(user_id,installation_id); remove token on logout |
| notification_preferences/jobs | user_id, category, consent_version, schedule, dedupe_key, state | UNIQUE(dedupe_key); consent recheck before delivery |
| reports | user_id, referenced_message/session, category, status, evidence_ref | restricted access and retention |
| privacy_operations | user_id, type, state, deadline, provider_steps | track export/deletion progress separately |
| deletion_tombstones | subject_id, scope, requested_at, expires_at | retained through backup horizon; no chat content |
| outbox / consumer_dedupe | event_id, aggregate, version, payload_ref, state | event_id unique; queue polling index |
| idempotency_requests | actor, route, key, request_hash, result_ref, expires_at | UNIQUE(actor,route,key) |
| admin_audit | actor, action, target_ref, reason, before/after redacted, timestamp | append-only restricted sink |

## DATA-02 — vector isolation pattern

Create embeddings only after choosing dimension/model; D below is a migration-time constant, not runtime SQL. Query owner/scope/status predicates in the database and validate recall under selective filters. pgvector supports exact and approximate search; select index type after measurement using the [official project documentation](https://github.com/pgvector/pgvector).

```sql
-- Illustrative migration/query; implementation supplies typed parameters and fixed D.
CREATE EXTENSION IF NOT EXISTS vector;
-- embedding vector(D) is created in the model-version-specific migration.
SELECT m.id, m.text
FROM memories m JOIN memory_embeddings_v1 e ON e.memory_id = m.id
WHERE m.user_id = :authorized_user_id
  AND e.user_id = :authorized_user_id
  AND m.companion_id = :authorized_companion_id
  AND m.deleted_at IS NULL AND m.status = 'active'
  AND (m.expires_at IS NULL OR m.expires_at > now())
ORDER BY e.embedding <=> :query_vector
LIMIT :bounded_k;
```

## DATA-03 — transactions, RLS and Redis

Apply row-level security to private tables as defense in depth. Runtime DB role must not own tables or bypass RLS. Set user context with SET LOCAL in each transaction; test pooled connections never retain another user's context. Background/admin roles have narrowly scoped audited bypass procedures where required. Composite owner FKs and application authorization remain necessary.

Redis uses environment-prefixed keys for rate limits, short-lived configuration, session leases, request dedupe caches and bounded hot context. Suggested TTLs: session lease 30s with renewal, config 60s, disposable context ≤10min. No long-term memory, final purchase or authoritative balance exists only in Redis. On Redis outage, durable quota transactions remain safe; paid voice admission may fail closed while read-only history remains available. Persist critical jobs in outbox or durable managed queue; Redis pub/sub is not a durable job queue.

Migrations use expand/backfill/contract, bounded batches and old/new code compatibility. Lock-sensitive changes require staging timing and rollback plan. Define deletion order and financial/legal exceptions explicitly. Partition messages/events only after index/storage measurements justify it.

Acceptance: constraints reject cross-owner children and duplicate grants; concurrent quota reservations cannot overspend; crash between DB commit and publication still delivers outbox work; pool/RLS tests isolate users; index plans meet latency budget on seeded scale data; restore applies deletion tombstones before serving requests.
