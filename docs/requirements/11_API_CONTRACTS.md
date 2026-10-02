# Backend API contracts

## API-01 — common rules

Implement executable OpenAPI 3.1 and JSON Schema 2020-12 under packages/contracts and generate C#/TypeScript clients. This document is the normative initial field inventory; schemas must reflect it and tests validate all examples. Prefix routes `/v1`. HTTPS only outside local; use bearer access tokens, backend identity mapping and per-resource ownership. Admin uses a separate audience/role boundary. Timestamps are RFC3339 UTC; IDs are opaque UUIDs; money uses integer minor units plus ISO currency, never float. Reject invalid enums, excessive lengths and unknown write fields; readers tolerate additive response fields.

List requests use opaque `cursor` and `limit` 1..100 (default 30), return `{items,next_cursor}` with null when done; order by stable timestamp+ID. Error response uses application/problem+json: `{type,title,status,code,detail,trace_id,retryable,field_errors}`; never expose stack traces, provider secrets or internal prompts. Map 400 invalid, 401 unauthenticated, 403 forbidden, 404 absent/inaccessible resource, 409 conflict, 412 ETag mismatch, 413 too large, 429 quota/rate limit with Retry-After, 503 unavailable. Avoid resource-enumeration leaks.

Mutating creation/action endpoints require `Idempotency-Key` (UUID) scoped to actor+route. Persist request hash and response/result pointer; same key/body returns same result, different body returns 409. Retain at least 7 days for user actions; durable store-event dedupe follows approved financial retention. PATCH/PUT use `If-Match` ETag over resource version. Retry timeouts with the same key; never imply exactly-once distributed delivery. All writes have server validation and audit-safe metadata.

## API-02 — resource and route inventory

| Method/path | Input → output | Authorization / behavior |
|---|---|---|
| GET /me | none → UserProfile | Authenticated owner |
| PATCH /me | display_name, locale → UserProfile | ETag, approved locale; no role fields |
| POST /me/consents | purpose, granted, policy_version → ConsentRecord | Owner; versioned evidence |
| GET /config | platform, app_version → ClientConfig | Public-safe flags, min version; no secrets |
| POST /companions | definition_id, name, pronouns, traits → Companion | Owner, limit checked |
| GET /companions | page → Companion[] | Owner only |
| PATCH /companions/{id} | name, pronouns, traits, voice_profile_id → Companion | ETag + allowlisted choices |
| POST /companions/{id}/reset-continuity | reset_scope → operation_id | Reauth; explicit UI confirmation |
| POST /conversations | companion_id → Conversation | Owner |
| GET /conversations | companion_id, page → Conversation[] | Owner |
| GET /conversations/{id}/messages | page → Message[] | Owner; canonical history |
| POST /conversations/{id}/messages | client_message_id, text → TurnAccepted | Owner, quota reservation |
| GET /turns/{id} | none → TurnState | Owner; reconciliation after reconnect |
| GET /turns/{id}/events | Last-Event-ID → SSE Event stream | Owner; bounded replay |
| POST /turns/{id}/cancel | reason → TurnState | Owner; idempotent |
| DELETE /conversations/{id} | none → operation_id | Owner; tombstone now, async purge |
| POST /voice/sessions | companion_id, conversation_id, capabilities → VoiceSession | Owner, quota/lease/region |
| POST /voice/sessions/{id}/renew | lease_version → lease/expires_at | Owner, bounded caps; no quota reset |
| POST /voice/sessions/{id}/end | reason → VoiceSummary | Owner; worker verifies final usage |
| GET /memories | companion_id, page → Memory[] | Owner; no raw vector |
| POST /memories | companion_id, text, type → Memory | Consent/policy, source=user_pinned |
| PATCH /memories/{id} | text → Memory | ETag; invalidate old embedding/context |
| DELETE /memories/{id} | none → operation_id | Immediate exclusion and context invalidation |
| GET /catalog | rig_family, locale, platform, page → CatalogItem[] | Public metadata; prices from store |
| GET /inventory | page → InventoryItem[] | Owner |
| PUT /companions/{id}/loadout | slot_items → Loadout | Owner, ETag, entitlement validation |
| GET /entitlements | none → EntitlementSnapshot | Owner; authoritative backend status |
| POST /purchases/sync | store, transaction_reference → EntitlementSnapshot | Owner; verify vendor, never trust claim |
| POST /webhooks/revenuecat | provider payload → acknowledgment | Dedicated verified provider auth |
| PUT /devices/{id}/push | token, platform, locale, timezone → Device | Owner; token encrypted, no logging |
| DELETE /devices/{id}/push | none → 204 | Owner; logout disables |
| PATCH /notification-preferences | categories, quiet_hours → Preferences | Owner, ETag |
| POST /reports | message_id/session_id, category, details → Report | Owner; minimal voluntary evidence |
| POST /privacy/exports | scope → operation_id | Recent auth, rate limit |
| POST /privacy/deletion | confirmation → operation_id | Recent auth; immediate access revoke |
| GET /operations/{id} | none → OperationStatus | Owner |

Admin APIs live under `/v1/admin`, require role permission per action and audit reason for writes. See admin spec. Identity sign-in/refresh/revocation uses selected OIDC SDK/provider contracts, wrapped by the client identity port; do not invent a second homegrown password service.

## API-03 — canonical types and examples

All resources include `id`, `version`, `created_at`, `updated_at` unless naturally immutable.

UserProfile: display_name (1..60), locale (allowlist), account_status, consent_versions. Companion: owner-scoped definition_id/version, name (1..40), pronouns (allowlist or moderated 1..40), traits object with five 0..1 numbers, voice_profile_id, rig_family and loadout_version. Conversation: companion_id, title (0..120), state active/deleting, latest_turn_at. Message: conversation_id, turn_id, role user/assistant, text (up to 8,000 input or 16,000 output characters), status accepted/streaming/completed/failed/cancelled/interrupted, source text/voice, client_message_id for user messages, heard_duration_ms when relevant, timestamps and sequence. Stored input length and model output token cap are both enforced.

```json
{
  "client_message_id": "11111111-1111-4111-8111-111111111111",
  "text": "Can we plan a relaxing evening?"
}
```

202 TurnAccepted:

```json
{
  "turn_id": "22222222-2222-4222-8222-222222222222",
  "user_message_id": "33333333-3333-4333-8333-333333333333",
  "status": "accepted",
  "events_url": "/v1/turns/22222222-2222-4222-8222-222222222222/events"
}
```

TurnState includes turn_id, conversation_id, status, assistant_message_id (nullable), last_seq, failure_code (nullable) and canonical text. One active text/voice generation per conversation; conflicts return 409 with active turn/session reference visible only to owner. Ordering is server-assigned; a client cannot choose role, model, owner, cost, emotion or entitlement in the send request.

VoiceSession response: id, transport enum livekit/mock/approved_direct, server_url, join_credential (redacted in logs; short-lived), credential_expires_at, lease_expires_at, session_deadline, room_id, playback_epoch, approved_audio_config and remaining_seconds estimate. IDs/room are generated by server. Credentials are never persisted to analytics. Renew cannot extend beyond authorized absolute session cap.

Memory: companion_id, text (1..1,000), type preference/fact/explicit_note, source_message_ids, confidence 0..1, status, expires_at nullable, editable true/false. InventoryItem: catalog_item_id, source purchase/subscription/free/grant, entitlement_id nullable, state active/revoked/expired. EntitlementSnapshot: version, fetched_at, valid_until, items with entitlement_id/state/expires_at; validity is a cache bound, not an unlimited offline purchase grant.

OperationStatus: id, type, state pending/running/completed/failed, requested_at, completed_at nullable, safe_error nullable and result_url nullable. Export URLs are short-lived authenticated/signed download references and never go in push/analytics. Privacy deletion returns operation reference before invalidating normal sessions; further status uses a narrowly scoped deletion receipt or approved support flow, not a still-valid general access token.

Acceptance: generated clients compile, all requests/responses validate, auth tests cover every route, replay creates one turn/purchase/equip, stale ETags fail without overwriting data, and a lost SSE connection reconciles through GET /turns without generating another response.
