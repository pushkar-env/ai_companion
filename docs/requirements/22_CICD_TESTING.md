# CI/CD and test strategy

## TEST-01 — pipeline stages

Every PR: formatting/static analysis, domain unit tests, generated contract drift/schema checks, migration validation, secret/dependency/license scans, backend integration with disposable Postgres/Redis, Unity edit-mode tests and relevant play-mode tests. Main/release: build IL2CPP Android/iOS, run smoke/contract tests, publish signed internal artifacts only through approved protected environments. iOS build/signing requires approved macOS/Xcode tooling and credentials; missing tools are a blocker, not a skipped pass.

Pin Unity/package/native library/toolchain versions and record reproducibility metadata. Produce SBOM, build provenance, crash symbols and release notes. Scan client bundles for server secrets. Infrastructure and database changes use staged previews and expand/contract migrations; no irreversible migration bundled invisibly into startup. Sign mobile releases through protected CI; restrict secrets to trusted branches/jobs and untrusted PRs to secret-free checks.

## TEST-02 — required suites

| Suite | Required scenarios and evidence |
|---|---|
| Domain | quota reservation/settlement races; relationship/reset rules; loadout compatibility; retention eligibility |
| Contracts | every endpoint, event and adapter; schema compatibility with previous supported app |
| Data/security | cross-user IDs/RLS/pool reuse; SQL injection; invalid auth; webhook forgery/replay; source-deletion derivatives |
| Integration | outbox crash/retry; provider timeout/429; lost SSE; duplicated billing; failed exports and dead letters |
| Unity | lifecycle cleanup, screen states, asset release, animation mixer, main-thread/audio constraints |
| Physical-device | permissions, Bluetooth, calls/background, thermal/memory, keyboard/safe areas, store sandbox and pushes |
| E2E | onboarding→chat→call→interrupt→memory→purchase→equip→restore→delete; account-switch isolation |
| Accessibility | VoiceOver/TalkBack and admin keyboard; text scale, reduced motion, contrast, caption-only path |
| Load/chaos | target streams/calls/RPS; 2x peak; provider outage; DB/Redis restart; queue backlog; catalog rollback |
| Recovery | PITR restore, deletion replay, reconciliation, deploy drain and secret rotation |

## EVAL-01 — AI and safety gates

Version synthetic/licensed eval datasets, model/config/prompt versions and grading rubric. Separate deterministic policy tests from model-scored judgments. Launch set: ≥300 multi-turn companion cases, ≥200 adversarial/safety cases and ≥100 memory retrieval/correction cases; overlap is permitted only if labeled. Include audio/accents/noise for launch locale. Sample repeated stochastic runs (at least three for critical cases). Use human adjudication on all critical failures and a stratified ≥50-case quality sample; an LLM judge alone cannot approve launch.

Initial gates: zero critical safety/privacy/tool-authorization failures; ≥95% task/policy adherence on agreed rubric; memory recall@8 ≥0.90 and zero cross-user retrieval; human-rated empathy/coherence/naturalness mean ≥4/5 with no unresolved severe sample failure. Track false refusals and sentiment manipulation; if thresholds conflict with real behavior, revise with owner approval and rationale, never lower silently. A model upgrade re-runs release evals and canary, not just API contract tests.

## TEST-03 — device and release matrix

Portrait is the required mobile orientation. M0 includes Editor layout checks at 360×640,
390×844 and 1080×1920, simulated safe areas/keyboard, long conversation scrolling and
larger text. M1 device evidence must additionally verify actual portrait lock, native IME
resize, notches/home indicators/gesture bars, background/resume and English/Hindi input.
Passing simulated geometry checks does not pass physical device or screen-reader criteria.

M1 selects named minimum/median/high Android and iPhone devices and supported OS versions, including lowest supported RAM, not emulator-only labels. Required networks: reference Wi-Fi (RTT ≤80ms, ≥10Mbps), cellular simulation (RTT 150ms, 1% loss, 1Mbps), degraded (RTT 300ms, 5% loss, 256kbps) and offline. Measure reference SLOs separately; degraded mode must remain safe/recoverable even when latency targets cannot be met. Test fresh install, upgrade from previous public version, reinstall and revoked permissions.

Store test evidence by requirement ID, build SHA, device/OS, network profile, timestamp, test steps/results and sanitized artifact references. CI reports skipped/blocked distinctly from passed. Release candidate needs P0 suites green, no Sev0/Sev1 issues and explicit disposition of other defects.

Acceptance: seeded failure is caught in each critical suite; an unknown schema or unapproved model cannot silently ship; physical-device and store sandbox evidence exist for both platforms; missing accounts/devices leave milestones incomplete; regression and canary evidence attach to each released version.
