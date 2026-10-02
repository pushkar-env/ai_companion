# Formal schema seeds

These application-owned JSON Schema 2020-12 definitions are concrete seeds for packages/contracts. They cover chat admission and the complete core text event stream. Extend the same conventions to the remaining resource/event tables in 11_API_CONTRACTS.md and 12_EVENT_SCHEMAS.md during M0. They are not vendor API schemas or a complete OpenAPI document. Enable format validation for UUID/date-time explicitly in the chosen validator; JSON Schema implementations may otherwise treat format as annotation only. Test cross-field rules separately.

## Chat send request and acceptance

Persist Idempotency-Key separately from client_message_id; the first identifies the HTTP action and the second prevents duplicate canonical user messages across transport retries. The URL conversation ID and authenticated user determine scope. This schema never accepts user_id or role from the client.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:companion:chat-contract:1",
  "$defs": {
    "SendMessage": {
      "type": "object",
      "additionalProperties": false,
      "required": ["client_message_id", "text"],
      "properties": {
        "client_message_id": {"type": "string", "format": "uuid"},
        "text": {"type": "string", "minLength": 1, "maxLength": 8000, "pattern": "\\S"}
      }
    },
    "TurnAccepted": {
      "type": "object",
      "required": ["turn_id", "user_message_id", "status", "events_url"],
      "properties": {
        "turn_id": {"type": "string", "format": "uuid"},
        "user_message_id": {"type": "string", "format": "uuid"},
        "status": {"const": "accepted"},
        "events_url": {"type": "string", "pattern": "^/v1/turns/[0-9a-fA-F-]+/events$"}
      }
    }
  },
  "$ref": "#/$defs/SendMessage"
}
```

Generate separate SendMessage and TurnAccepted schema entrypoints from these definitions. Verify events_url resolves only to the expected API origin and its ID matches turn_id; never fetch an arbitrary response-provided host. Whitespace-only input is rejected before spending. Normalize storage consistently without changing text offsets after streaming.

## Text event stream

The following discriminated union covers every core text-stream event in this version. Optional unknown fields are allowed in server responses for forward-compatible readers. Producers use stricter generated serializers and allowlisted payloads so sensitive fields cannot leak through this permissiveness. Unknown event types are ignored safely by clients, but server producers must not emit undeclared types.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:companion:text-event:1",
  "type": "object",
  "required": ["event_id", "schema_version", "type", "aggregate_id", "seq", "occurred_at", "trace_id", "payload"],
  "properties": {
    "event_id": {"type": "string", "format": "uuid"},
    "schema_version": {"const": 1},
    "type": {"type": "string"},
    "aggregate_id": {"type": "string", "format": "uuid"},
    "seq": {"type": "integer", "minimum": 0},
    "occurred_at": {"type": "string", "format": "date-time"},
    "trace_id": {"type": "string", "minLength": 1, "maxLength": 128},
    "payload": {"type": "object"}
  },
  "oneOf": [
    {
      "properties": {
        "type": {"const": "turn.accepted"},
        "payload": {
          "required": ["user_message_id"],
          "properties": {"user_message_id": {"type": "string", "format": "uuid"}}
        }
      }
    },
    {
      "properties": {
        "type": {"const": "turn.text.delta"},
        "payload": {
          "required": ["text", "offset"],
          "properties": {
            "text": {"type": "string", "minLength": 1, "maxLength": 2000},
            "offset": {"type": "integer", "minimum": 0, "maximum": 16000}
          }
        }
      }
    },
    {
      "properties": {
        "type": {"const": "turn.expression"},
        "payload": {
          "required": ["schema_version", "expression", "valence", "arousal", "intensity", "gesture", "duration_ms"],
          "properties": {
            "schema_version": {"const": 1},
            "expression": {"enum": ["neutral", "warm", "curious", "thoughtful", "concerned", "celebratory"]},
            "valence": {"type": "number", "minimum": -1, "maximum": 1},
            "arousal": {"type": "number", "minimum": 0, "maximum": 1},
            "intensity": {"type": "number", "minimum": 0, "maximum": 1},
            "gesture": {"enum": ["none", "small_nod", "small_shake", "open_hand"]},
            "duration_ms": {"type": "integer", "minimum": 100, "maximum": 5000}
          }
        }
      }
    },
    {
      "properties": {
        "type": {"const": "turn.completed"},
        "payload": {
          "required": ["assistant_message_id", "text", "finish_reason"],
          "properties": {
            "assistant_message_id": {"type": "string", "format": "uuid"},
            "text": {"type": "string", "maxLength": 16000},
            "finish_reason": {"enum": ["stop", "length", "safe_fallback"]}
          }
        }
      }
    },
    {
      "properties": {
        "type": {"const": "turn.failed"},
        "payload": {
          "required": ["code", "retryable", "partial_text"],
          "properties": {
            "code": {"type": "string", "minLength": 1, "maxLength": 80},
            "retryable": {"type": "boolean"},
            "partial_text": {"type": "string", "maxLength": 16000}
          }
        }
      }
    },
    {
      "properties": {
        "type": {"const": "turn.cancelled"},
        "payload": {
          "required": ["reason", "partial_text"],
          "properties": {
            "reason": {"enum": ["user", "session_ended", "policy", "quota", "superseded"]},
            "partial_text": {"type": "string", "maxLength": 16000}
          }
        }
      }
    }
  ]
}
```

Validate total text length and offset continuity across deltas, exactly one terminal outcome per turn, monotonic sequence and owner authorization outside per-event schema. Duplicate delivery is legal but double application is not. The wire byte cap of 32 KiB applies after UTF-8 JSON serialization. For v1, bound emitted/stored assistant text by both 16,000 Unicode code points and a 24 KiB JSON-encoded text budget, leaving envelope headroom. Stop at a valid character boundary with finish_reason=length before exceeding either limit; the terminal full text and canonical stored text must match the accepted deltas. Enforce the final serialized event byte limit separately because escaping affects size. Larger responses require a future compatible chunk/fetch design, not silent truncation of only the completion event.

## Contract test seeds

Positive: documented send request and TurnAccepted example; text.delta example from the events spec; each terminal variant; valid neutral expression. Negative: blank text, extra role/user_id in send, negative sequence/offset, malformed UUID/time, unknown expression, range overflow, missing terminal field and envelope over 32 KiB. Stateful: duplicate same ID, same seq/different payload, noncontiguous offset, cancellation followed by late delta, expired replay window, and multi-code-point Unicode text consumed by C# and TypeScript clients. Every stateful failure needs a defined recovery path, not undefined behavior.
