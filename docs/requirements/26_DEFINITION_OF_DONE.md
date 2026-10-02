# Definition of done and requirement traceability

## DONE-01 — feature done

A feature is done when its happy path, empty/error/offline/permission states, accessibility, authorization, bounded retries, observability and data lifecycle are implemented; acceptance tests pass; migrations/config/contracts/docs are updated; no secrets/temporary production mocks exist; and a reviewer can reproduce the result. Device-sensitive behavior needs physical Android and iOS evidence. If assets, credentials, legal approvals or devices are missing, the status is partial/blocked with exact reason.

## DONE-02 — production done

All P0 feature criteria plus security/privacy/safety review, provider/account approvals, signed store builds, performance/capacity/cost evidence, recovery/rollback rehearsals, support/on-call ownership and authorized rollout are mandatory. No Sev0/Sev1 defects. A lower severity exception records owner, impact, mitigation, deadline and explicit acceptance. Unit tests alone cannot prove consumer polish, lip sync, store behavior, accessibility or production scale.

## Traceability matrix

The implementation repository must expand each row into test-case IDs with actual evidence links. This table maps all normative requirement groups; it does not claim they have passed.

| Requirement groups | Primary milestone | Evidence owner / required proof |
|---|---|---|
| AGENT-01..04, HUMAN-01..03 | M0 onward | Implementer/product: logs, answered decision gates, resumed work |
| PROD-01..04 | M2–M7 | Product/design: full journey and state review, approved scope |
| ARCH-01..03, REPO-01..02 | M0/M5 | Engineering: ADRs, bootstrap, restart and adapter tests |
| UNITY-01..03 | M1/M3/M5 | Mobile: physical AOT/device/lifecycle evidence |
| AVATAR-01..02, FACE-01..02 | M1/M3/M4 | Art/mobile: validator, calibrated visemes and captures |
| VOICE-01..04 | M1/M3 | Voice/mobile: interruption, reconnection, orphan/cost tests |
| AI-01..03 | M2/M3 | AI: contract suites, policy/tool evals and failure fixtures |
| PERSONA-01, EMOTION-01, REL-01 | M3 | Product/AI: coherence, valid metadata, non-coercive progression |
| MEM-01..03 | M2/M3/M5 | Backend/privacy: retrieval, deletion, active-context tests |
| ASSET-01, WARD-01 | M4 | Art/mobile/backend: outfit QA, ownership, catalog rollback |
| API-01..03, EVENT-01..03, DATA-01..03 | M0/M2/M5 | Backend: generated schemas, isolation/replay/transaction tests |
| AUTH-01, SEC-01..02 | M2/M5 | Security: threat model, account/link/revocation tests, assessment |
| BILL-01..03 | M4/M6 | Commerce: both store sandboxes and reconciliation evidence |
| NOTIFY-01..02 | M5 | Mobile/product: consent, tokens, deep links, timezone tests |
| SAFE-01..02, PRIV-01..02 | M2–M7 | Safety/legal/privacy: policy signoff, evals, export/deletion |
| OBS-01, ANALYTICS-01 | M5 | Operations/data: dashboards, alert drills, redaction/consent |
| ADMIN-01..02 | M5 | Admin/security: RBAC, accessibility and mutation audit |
| INFRA-01..03, ENV-01..03 | M0/M5 | Operations: IaC, config/secrets, backups/restore |
| TEST-01..03, EVAL-01 | Every milestone | QA/AI: recorded pass/fail/blocked evidence |
| PERF-01, RELIABILITY-01, CAPACITY-01, COST-01 | M1/M5/M6 | Engineering/finance: benchmarks, SLOs, forecasts/ledger |
| FLAG-01, RELEASE-01 | M5/M7 | Release operator: canary, kill switch, compatibility |
| PLAN-01..02, DONE-01..02, LAUNCH-01 | M0–M7 | Product/release: complete evidence and authorization |

## Evidence record template

Requirement/test ID; status (`not_started/in_progress/blocked/pass/fail/accepted_exception`); build SHA/config/model version; environment; device/OS/network where relevant; dataset version/sample size; execution date; sanitized output/capture; reviewer; unresolved issue; human approval reference if required. The executing agent may author technical evidence but must not forge a human/legal/store approval.

Acceptance: every normative ID has an evidence entry before launch; critical suites report real failures and missing prerequisites; launch checklist links completed evidence rather than blanket claims.
