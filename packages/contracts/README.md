# Application-owned contracts v1

`chat.schema.json` is the text contract source; `resources.schema.json` scaffolds the
remaining resource and client/internal event inventory. `openapi.json` describes only
the four executable mock operations. Future endpoints are not silently represented as implemented.

Generate: `python tools/build-contracts.py`; verify: `python tools/build-contracts.py --check`.
Generated C#/TypeScript DTOs and typed client facades use the same schema/OpenAPI input.
The Unity output is a generated copy; do not edit it independently. Client facades take
an injected transport and expose typed operations/cancellation without vendor dependencies.
Their SSE operation returns wire text; production streaming transport/authorization is M2.
The seed/extension scripts document the initial schema construction; normal edits belong
in the schemas. Do not rerun seed scripts over deliberate schema revisions.

Strict write schemas reject unknown fields. Response readers tolerate additive fields;
producer payloads contain only allowlisted contract values. JSON Schema format validation
is explicitly enabled including RFC3339 and UUID. Cross-event offsets use Unicode code
points; sequence gaps terminate the local attempt with an explicit retry recovery path.

Resource/event scaffolds are not legal/provider decisions or working production routes.
Voice credentials are contract fields only; no credential values are present. Remaining
cross-resource ownership, media epochs, durable replay expiry and API policies need M2/M3
implementation and integration evidence. No generated DTO by itself proves validation.
