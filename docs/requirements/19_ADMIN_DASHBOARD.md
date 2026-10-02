# Administration, support and content operations

## ADMIN-01 — role-limited operations

Build a responsive React/TypeScript admin using generated API clients and accessible controls. No public registration. Roles: support (minimal account/entitlement view), safety reviewer (assigned reports), catalog editor (draft assets/products), operator (service flags/health), privacy operator (export/deletion tracking), finance (reconciliation), administrator (role management). Enforce permissions server-side; hiding a button is not authorization. Require SSO/MFA and short-lived sessions.

Views: service/cost health; pseudonymous account search; verified purchase state and restore diagnostics; report queue; privacy operation status; catalog draft/preview/release; prompt/persona version registry and evaluation results; feature flags; job/dead-letter queue; audit history. Do not show full chat history by default. Sensitive evidence access requires assigned case, reason, explicit permission and audit; hide unrelated messages. Production impersonation is disabled initially.

## ADMIN-02 — guarded mutation workflow

Admin routes include GET /admin/health, GET /admin/accounts/{id}/support-summary, GET/PATCH /admin/reports/{id}, GET /admin/privacy-operations, POST /admin/catalog/releases, POST /admin/flags/{key}/changes and POST /admin/jobs/{id}/retry. Writes require reason, idempotency key, version check and permission. Global safety/price/entitlement changes and production deletion/bulk actions need a reviewable preview and explicit authorized approval; record actor and approval reference. Grant/refund operations must use an approved ledger/provider workflow, never arbitrary table edits.

Draft→reviewed→published versions for catalog and prompt/config changes; no production editing in place. Use side-by-side diff, affected cohort count and rollback target. Emergency kill switches may be operated by authorized incident responders within preapproved scope; preserve immutable audit.

Acceptance: permission matrix tests every role/action including direct API calls; unauthorized evidence stays inaccessible; concurrent edits return conflict; each mutation has auditable actor/reason/version without secrets; keyboard/screen-reader workflows pass; support can diagnose a failed restore using identifiers without reading private chat.
