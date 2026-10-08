# Google Play readiness — 2026-10-08

## Verdict and audit scope

**Not ready for public release.** We have an Editor-tested companion experience and
local backend foundations. We do not yet have a production-connected Android app,
verified release AAB, physical-device acceptance or completed release operations.
Uploading an internal-test bundle and releasing publicly are different gates.

This is a requirements/code/evidence audit, not a new runtime test run. Reviewed the
normative groups in requirements/01–27, STATUS, MEMORY, decisions, current Unity
presentation/build settings, local account/voice services and evidence records.
Existing test counts refer to their recorded revisions, not a fresh full-suite pass.
No percentage is assigned: visual polish, backend foundations and store readiness have
different acceptance criteria. No Play Console account or external deployment was inspected.

Focus is Android/Google Play. iOS remains in the product specification but is not a
Google Play submission dependency. This audit does not silently remove iOS, Hindi,
memory, realtime calls or commerce from the agreed product scope. Optional reminders
and additional avatar families are P1; P0 omissions need an explicit scope decision.

## What is implemented and what remains

“Local verified” means recorded Editor/local integration evidence, not release-ready.
“Partial” includes implemented subsets with missing production/device acceptance.

| Requirements | Current evidence/status | Remaining before the specified public product |
|---|---|---|
| PROD-01..04 | Partial: chat and customization work; adults-only, non-explicit, India, English/Hindi direction recorded | Eligibility/consent/onboarding, guest limits, complete account journey, report/copy/privacy/help, persistent draft recovery, full state and accessibility review |
| UNITY-01..03 | Local verified subset: portrait, safe-area/keyboard simulations, lifecycle cancellation, permission UI | Actual Android IME/Back/audio focus, Bluetooth/phone interruptions, process death, secure account cache, release AOT and native permissions |
| AVATAR-01..02, FACE-01..02 | Alita, expressions/lip movement, fingers, gaze, foot shifts, stretches/yawn and reduced motion verified locally | Mobile LOD/draw consolidation, complete shipping rig validation, acoustic lip-sync evidence, all release outfit/gesture combinations on devices |
| ASSET-01, WARD-01 | Local verified preview: two tops/two bottoms, signature dress, tints, six skin tones, Save/Cancel | Server-owned inventory/equip, compatibility validation, asset delivery/hash/cache/rollback, authoritative cross-device appearance |
| VOICE-01..04 | Local dictation review, streaming sentence speech, Replay/Stop; synthetic session/receipt checks | Production mobile STT/TTS and realtime transport, true duplex/barge-in, route changes, durable session watchdog, reconnect and actual cost reconciliation |
| AI-01..03 | Local Ollama text/emotion and streaming decoder; adapter/schema groundwork | Approved hosted/provider configuration, moderation before text/audio delivery, secure tools/context, provider outages/caps and production usage integration |
| PERSONA-01, EMOTION-01, REL-01 | Basic character behavior and bounded local emotion output | Versioned personality controls/presets, coherence evaluation, approved continuity/reset rules; no paid-affection mechanics |
| MEM-01..03, DATA-02 | Specified; no completed production memory implementation evidenced | Opt-in manager, provenance, extraction/retrieval, corrections, consent epochs, derivative deletion and active-context invalidation |
| API-01..03, EVENT-01..03 | Generated contracts/mock routes; local history/SSE replay/cancel integration | Full production route coverage, authenticated adapters, mobile release serialization, approved errors/limits/version compatibility |
| DATA-01, DATA-03, ARCH-02 | Real local PostgreSQL: RLS, atomic admission, outbox, quota settlement, lease fencing and canonical replay | Join real AI execution to durable history/usage; production migrations/roles, reconciliation, privacy jobs and restore tests |
| AUTH-01, SEC-01..02 | Synthetic expiring identity and owner-isolation tests only | OIDC, secure device credentials, guest linking, logout/revocation/recovery, threat model, dependency/secret review and security assessment |
| BILL-01..03 | Quota primitives; no production store purchase integration | Approved prices/allowances, Play billing/RevenueCat setup, server entitlements, purchase/restore/refund/renewal/account-switch sandbox proof |
| SAFE-01..02, PRIV-01..02 | Scope constraints and policy specifications; no completed release safety/privacy system | Moderation, per-response reporting, age/AI disclosures, reviewed terms/retention/consent, export and in-app/web account deletion, processor/backup propagation |
| NOTIFY-01..02 | Specification only; reminders are P1 | If included: opt-in categories, safe copy, quiet hours, logout/deletion races, FCM and authorized deep links; do not block V1 merely on optional reminders |
| OBS-01, ANALYTICS-01 | Local diagnostic evidence; no deployed operational monitoring | Redacted crashes/ANRs/traces, dashboards/alerts, consent-aware analytics, named responders and tested alert routing |
| ADMIN-01..02 | apps/admin is a reserved boundary, not an application | Restricted support/report/entitlement tools, MFA/RBAC, audit and guarded mutations |
| ARCH-01/03, INFRA-01..03, ENV-01..03 | Local scaffolding and disposable DB harness | Approved topology/regions, HTTPS service deployment, secrets, environment separation, IaC, backup/restore and incident drills |
| REPO-01..02, TEST-01..03 | Many local suites and authored CI; old isolated Android build evidence | Exact-candidate clean checkout/build, hosted CI evidence, complete regressions, physical device/network matrix, install/upgrade tests |
| EVAL-01 | Evaluation requirements exist; no frozen release-quality corpus/results evidenced | 300 companion, 200 adversarial, 100 memory cases per spec; repeated critical cases and human quality/safety review including Hindi |
| PERF-01, RELIABILITY-01, CAPACITY-01, COST-01 | Desktop measurements and local quota concurrency checks | Physical 30-minute thermal/RSS/frame runs, cold starts, measured AV latency, workload forecast, 2x peak/soak, provider budgets and SLO telemetry |
| FLAG-01, RELEASE-01 | Release strategy specified | Server-enforced gates, emergency stop, compatibility/catalog rollback, staging rehearsal and measured staged rollout |
| AGENT-01..04, HUMAN-01..03, PLAN-01..03, DONE-01..02, LAUNCH-01 | Instructions/decisions/evidence maintained; gates remain open | Complete per-ID release evidence, responsible human approvals and exact-build release authorization |

### Evidence anchors

- UI: [transparent overlay](evidence/transparent-chat/README.md),
  [text fix](evidence/text-rendering/README.md), [UI polish](evidence/ui-polish/README.md).
  111 combined overlay/portrait/streaming/lifecycle checks were recorded for ADR-066;
  the subsequent font fix has separate native/1x visual verification.
- Character: [expressive idle](evidence/expressive-idle/README.md),
  [wardrobe](evidence/wardrobe/README.md), [skin tones](evidence/skin-tones/README.md).
- Interaction: [streaming](evidence/streaming-presence/README.md),
  [lifecycle](evidence/mobile-lifecycle/README.md),
  [permissions](evidence/microphone-permission/README.md).
- Data: [durable database](evidence/m2/database/verification.md),
  [history integration](evidence/m2/unity-account-api/README.md),
  [virtualization](evidence/m2/history-virtualization/README.md).
  Latest recorded database/API/Unity harness: 114 groups, with separate history UI suites.

## Concrete release gaps found in source

1. `services/voice-agent/talking-character.mjs` is a Windows Editor development
   service using loopback Ollama and installed Windows speech. It is not a shipping
   Android AI service. Normal chat remains session-only.
2. `services/account-api/Program.cs` explicitly requires local synthetic-account
   configuration and binds loopback. Real PostgreSQL durability is useful groundwork,
   but the history lab is not user login or persisted normal-chat integration.
3. `AndroidDiagnosticBuild.cs` builds CCCharacterTest as a development APK with
   debug signing and a test identity. The latest recorded build is September 27:
   **171.8 MB**, ARM64 IL2CPP, **0 errors / 998 warnings**. It predates the current
   TalkingCompanion UI/wardrobe/streaming changes and was not physically tested.
   [Exact historical evidence](evidence/m1/android/build-20260927-195903/verification.md).
4. Source PlayerSettings still has a Unity-template Android application ID,
   version 0.1.0/code 1 and automatic target SDK (`AndroidTargetSdkVersion: 0`).
   Automatic is not proof of the packaged target SDK. No verified release AAB,
   final package identity, upload-key workflow or Play App Signing evidence exists.
5. Alita's recorded full rig is 90,595 triangles / 24 material slots versus the
   specification's 70k LOD0 / 12 skinned-draw targets. Slots are not a measured draw
   count; current worst-case outfit, native GPU time and draw calls must be profiled.
6. `apps/admin`, `infra` and evaluation/load directories contain planning boundaries,
   not evidence of deployed support, production infrastructure or release evals.

## Ordered path to Google Play

| Order | Deliverable and exit gate | Dependency / owner |
|---|---|---|
| 1 | Define release configuration and one integrated mobile architecture: client → authenticated API → moderated AI/voice → durable history/usage. Keep a synthetic adapter for tests. | Engineering; owner Q-005/006/007/008 decisions before real provider/data deployment |
| 2 | Complete first-user and account journeys: AI/age/consent, guest entry, sign-in, saved normal chat, secure credentials, offline/process-death recovery, settings/help/report/export/delete. | Engineering; identity/privacy/guest policy approval |
| 3 | Connect approved text/voice services and safety controls; prove streaming, cancellation, reconnect and usage settlement. Implement opt-in memory/personality and evaluate English/Hindi. | Engineering/AI; approved providers, voice rights, policy and budgets |
| 4 | Finish production wardrobe/inventory and approved billing; test purchase/restore/refund/revocation and cross-device ownership. | Engineering; product/pricing/store accounts. Commerce remains P0 unless explicitly deferred |
| 5 | Optimize and validate the actual Android app: LOD/assets, release AOT, native keyboard/mic/audio focus, TalkBack/200% text, low-memory/process death, network changes, 30-minute thermal/performance runs. | Engineering/QA; resume previously owner-deferred physical tests and agree device tiers |
| 6 | Deploy staging/production with redacted monitoring, safety/support admin, rate/spend caps, backups/deletion, security/load/eval suites and rehearsed kill switches. | Engineering/operations; approved infrastructure and named support/release owners |
| 7 | Produce signed release AAB and store materials; validate through internal testing, then approved closed beta. Fix pre-launch/real-user defects and gather required test evidence. | Engineering + Play Console owner; exact build, privacy/listing and test access |
| 8 | Complete LAUNCH-01, obtain explicit release authorization and roll out with monitored health/cost/safety gates. | Release owner; Google review/production access |

Steps overlap where independent. An internal test can happen before all public P0
features are finished; it still needs an honest, usable test build and safe data handling.
Internal testing is not public-launch acceptance. No date estimate is defensible until
provider, release scope and device-test dependencies are resolved.

**Next safe engineering slice:** implement a mobile-capable service/configuration
boundary and durable normal-chat integration behind test adapters, with explicit
unconfigured states. Prepare the exact provider/identity/deployment proposal in parallel.
Do not expose the existing local development services publicly as a shortcut.

## Google Play checks (official sources checked 2026-10-08)

| Gate | Requirement / action | Current status |
|---|---|---|
| Target SDK | New phone apps must target Android 16 / API 36 or higher under the current rule. Verify the built manifest, not just Unity's automatic setting. [Google target API policy](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en) | Unverified |
| Native compatibility | Verify every native library, packaging alignment and runtime behavior for 16 KB pages. Current Android guidance requires support for API 35+ on 64-bit devices and lists February 1, 2027 update enforcement; check Console applicability at submission. [Android guide](https://developer.android.com/guide/practices/page-sizes) | Unverified |
| Distribution identity | Final permanent package name, signed AAB, version codes, Play App Signing and secure upload-key custody. [Google app setup](https://support.google.com/googleplay/android-developer/answer/9859152?hl=en) | No release evidence |
| AI content | Prevent restricted generated content and provide in-app reporting/flagging without leaving the app. [AI content policy](https://support.google.com/googleplay/android-developer/answer/13985936?hl=en) | Not completed |
| User data | Accurate privacy policy and Data safety disclosures covering actual SDK/provider flows; in-app and external account-deletion routes when account creation is offered. [User data policy](https://support.google.com/googleplay/android-developer/answer/10144311) / [deletion details](https://support.google.com/googleplay/android-developer/answer/13327111?hl=en) | Not completed |
| Console/test eligibility | Confirm account type, verification and production access. Personal accounts created after November 13, 2023 require at least 12 continuously opted-in closed testers for 14 days before applying for production access; meeting the minimum is not automatic approval. [Google testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en) | Account type/access unknown |
| Review package | App icon/screenshots/descriptions, content rating, target audience, ads declaration, support/privacy/deletion URLs, app-access instructions and current applicable permission/billing declarations. | Not evidenced; validate in actual Console |

Recheck policy and SDK requirements on the actual submission date. This audit is not a
store approval or legal certification; signed build contents and Console review decide
submission eligibility.

## Owner decisions already recorded / still needed

Approved: adults-only non-explicit companion, India, English/Hindi; Alita-only direction;
rights confirmation for supplied assets; guest trial with account required for saved
history and purchases; warm-room transparent portrait UI.

Open: Q-004 device tiers/testing; Q-005 prices/free quotas/spend; Q-006 AI/voice providers
and processing terms; Q-007 hosting/identity; Q-008 retention/consent/deletion policy;
Q-009 store/signing ownership; Q-012 purchase transfer rules; Q-013 continuity/engagement;
Q-014 support/safety/privacy ownership; Q-015 launch forecast. Q-010 still needs a
complete shipping-asset/voice rights inventory despite supplied-asset confirmation.
Q-016's broad multi-character question is narrowed by Alita-only direction, not approval
of every possible identity/body feature. See [decision log](requirements/QUESTIONS.md).

This review requests no secrets, purchases or uploads and does not reopen the prior
physical-testing deferral automatically.
