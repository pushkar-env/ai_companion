# Production delivery backlog

Updated: 2026-10-08. Owner requested sequential implementation through production.
Source: [readiness audit](PLAY_STORE_READINESS.md), requirements/01–27 and recorded
owner decisions. This is a work queue, not permission to publish or buy services.

## Tracking rules

- Work one implementation subtask at a time. Split a task further when its acceptance
  cannot be demonstrated in a coherent change. Continue independent work around gates.
- States: queued, active, verified-local, awaiting-external, release-verified.
  A local test does not close its corresponding mobile/production acceptance gate.
- On completion record changes, exact checks/evidence and remaining limits here, in
  STATUS.md and MEMORY.md; record architecture/scope decisions in DECISIONS.md.
- Preserve existing UI/assets/GUIDs, portrait and interactive Editor layout. Physical
  testing remains owner-deferred; no credentials in chat, paid provisioning or publishing
  without the recorded specific approvals. No P0 requirement is removed by this queue.

## Ordered subtasks

Unless stated otherwise implementation owner is engineering; approval owners remain
those in requirements/QUESTIONS.md. Dependencies identify prerequisites, not completion.

| ID | Subtask / acceptance | Dependencies | State |
|---|---|---|---|
| P01 | Isolate local connection/configuration from chat; fail closed on unsupported builds, preserve drafts and reject unsafe endpoints/redirects | None | verified-local |
| P02 | Define mobile service/session composition and durable normal-chat adapter; explicit synthetic test mode, canonical reconciliation and cancellation | P01; API/EVENT/DATA | verified-local |
| P03 | Integrate normal chat with local durable account API; one canonical message after retry/reconnect and no cross-account display | P02 | queued |
| P04 | Prepare reviewable provider/identity/hosting/region/cost proposal and configuration matrix | Q-005/006/007/008; owner decision | queued |
| P05 | Production identity: OIDC, secure device credentials, rotation/logout/revocation/recovery | P04 approval, AUTH/SEC | awaiting-external |
| P06 | Guest entry, account linking and saved-history boundaries; approved limits/transfer consent | P05, Q-011 policy details | queued |
| P07 | Production conversation API/worker: authenticated provider dispatch, durable events, usage reconciliation | P03/P04/P05, AI/DATA | queued |
| P08 | Age/AI disclosure, onboarding, consent versions and voice preview; first text without mic/payment | Q-008, PROD/SAFE | queued |
| P09 | Offline/restart/process-death draft and delivery recovery; account-scoped encrypted cache | P03/P05, Q-008 | queued |
| P10 | Copy/report assistant turns and support/help routes; report durable delivery and owner workflow | SAFE/PROD, Q-014 | queued |
| P11 | Moderated streaming before UI/audio, safe failure handling, prompt/tool authorization | P04/P07, AI/SAFE | queued |
| P12 | Text-to-speech/speech-to-text mobile providers; cancellation, captions, English/Hindi | P04/P11, voice rights | queued |
| P13 | Realtime call transport and lifecycle, barge-in, reconnect, watchdog/orphan cost controls | P07/P12, VOICE | queued |
| P14 | Native Android audio focus/Bluetooth/phone interruptions and permission revocation | P12/P13, device tests | queued |
| P15 | Versioned bounded personality controls and neutral fallback; non-coercive continuity/reset | PERSONA/EMOTION/REL, Q-013 | queued |
| P16 | Memory consent/manager, pinned facts, edit/delete/source provenance | MEM, Q-008 | queued |
| P17 | Memory extraction/retrieval isolation, corrections and poisoned-memory tests | P07/P11/P16 | queued |
| P18 | Memory tombstones, summaries/vectors/cache/active-call invalidation and retry safety | P13/P17 | queued |
| P19 | Export/account deletion API and app/web routes, revocation and processor purge tracking | P05/P18, approved retention | queued |
| P20 | Restore/backups must replay deletions before traffic; prove privacy job durability | P19/P35 | queued |
| P21 | Server catalog/inventory/equip with ownership/version/rig validation and account sync | P05, ASSET/WARD | queued |
| P22 | Addressable asset delivery, hashes, cancellation/low disk/cache/base fallback and catalog rollback | P21, approved hosting | queued |
| P23 | Full shipping wardrobe/gesture compatibility and rights inventory | P21/P22, Q-010 | queued |
| P24 | Approved products/pricing/allowances/transfer policy and reviewable purchase mapping | Q-005/009/012 | awaiting-external |
| P25 | Store purchase UI and backend-authoritative grants, webhook verification/reconciliation | P05/P21/P24 | queued |
| P26 | Sandbox purchase/renewal/grace/refund/restore/account-switch and quota enforcement | P25, store accounts | queued |
| P27 | Mobile character LOD, materials/draws and texture budgets; preserve art/rig fidelity | AVATAR/PERF, existing Alita | queued |
| P28 | TalkBack/200% text, focus/contrast/touch targets, keyboard/Back/safe-area and long chat | UNITY/PROD, native QA | queued |
| P29 | Hindi text/input/recognition/voice and bilingual conversational quality | P11/P12/P28 | queued |
| P30 | Exact app Android release build pipeline, AOT/stripping/serialization, native 16 KB checks | P01/P07, release config | queued |
| P31 | Device matrix: install/upgrade, lifecycle, offline/network, memory, 30-minute thermal, cold start, AV latency | P14/P27–30, Q-004; testing deferred | awaiting-external |
| P32 | Versioned AI release corpus and human grading: companion/safety/memory/locales | P11/P15/P17/P29, EVAL | queued |
| P33 | Threat model, SBOM, scans, auth/IDOR/abuse tests and security review dispositions | P05/P07/P19/P25, SEC | queued |
| P34 | Redacted crashes/ANRs/traces/metrics, consent-aware analytics, dashboards/alert drill | OBS/ANALYTICS, approved services | queued |
| P35 | Environment-separated deployment/IaC/secrets, migrations, backups/restore | P04 approval, INFRA/ENV | queued |
| P36 | Support/safety admin: MFA/RBAC, reports/privacy/entitlements and audit/versioned writes | P10/P19/P25/P35, Q-014 | queued |
| P37 | Forecast/load/soak/provider quota tests, actual unit cost/spending caps and reconciliation | P07/P13/P26/P34/P35, Q-005/015 | queued |
| P38 | Server-enforced flags, generation kill switch, drain and rollback/incident rehearsals | P34–37, FLAG/RELEASE | queued |
| P39 | Clean checkout + hosted CI, exact-candidate full regression and release provenance | REPO/TEST, shipping feature set | queued |
| P40 | Final brand/package ID, verified Play account, signing/upload-key custody and release AAB | P30/P39, Q-009 | awaiting-external |
| P41 | Reviewed policies, Data safety/ratings/declarations, store assets and reviewer access | P19/P23/P40, responsible reviewers | queued |
| P42 | Internal test then authorized closed beta; pre-launch report and real feedback closure | P31–41; owner/cohort approval | awaiting-external |
| P43 | LAUNCH-01 exact-build evidence review; public authorization and monitored staged rollout | P42; release owner + Google review | awaiting-external |
| P44 | Optional P1 notifications/reminders with consent/quiet hours/deletion and deep-link checks | Q-013; not a V1 blocker by default | queued |
| P45 | iOS native/build/store acceptance for the broader product; separate from Google Play | Mac/iPhone/accounts and corresponding gates | awaiting-external |

## Completion log

### P01 — verified-local, 2026-10-08

Connection-source boundary, Windows-Editor-only credential reader, canonical local
endpoint validation, centralized bounded/nonredirecting requests, safe unconfigured
player guidance, draft-preserving send/retry and microphone preflight implemented.
18 contract + 14 Editor boundary + 14 live streaming checks passed. [Evidence](evidence/production/p01/README.md).
Initial/final Play stopped, scene clean, no layout or existing GUID changes. P01 does
not close physical mobile, production auth or provider gates. P37 must investigate
31.244s first-text observation; do not claim a latency pass.

Next: P02 (service/session composition and durable command adapter), then P03 (normal
chat integration). Existing backend admission uses a client_message_id + Idempotency-Key;
reuse both for ambiguous retries, reconcile server truth, and keep synthetic mode explicit.
Do not send synthetic history or local chat to a hosted provider before approval.


### P02 — verified-local, 2026-10-08

Added SyntheticConversationCommands beside the history/SSE adapter. Immutable pending
text and retry IDs; admission reconciliation, versioned cancellation, uncertain local
abort and rejected-session gating. Explicit synthetic account/conversation scope only.
16 real Unity/API/PostgreSQL checks passed; enclosing harness 114 groups includes the
Unity result gate. [Evidence and P03 composition contract](evidence/production/p02/README.md).
Editor remained stopped, scene/layout untouched; fixture removed and test cluster stopped.
No normal-chat change, production provider, auth or process-death persistence claim.

Next: P03, connect command/history adapters to an explicitly labeled synthetic normal-chat
mode and verify canonical rendering/retry/account-switch cleanup. P05/P07 supply production
identity and actual durable AI later. Earlier “Next P02” entry is historical.
