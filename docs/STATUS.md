# Implementation status

Updated: 2026-10-05. Active work: **M1 partial; independent M2 durable conversation foundations underway**. Playable local AI talking-character scene verified in Editor; mobile/production-provider evidence pending.
M0 remains implemented with partial / blocked verification as detailed below.

## Current production-directed progress

- **Unity synthetic history adapter (2026-10-05):** engine-independent event projection/
  UTF-8 decoder and UnityWebRequest history/SSE transport are implemented without new
  packages. **9 stopped-Editor checks passed** using real loopback HTTP faults. Existing
  talking scene remains session-only; portrait account/history UI and actual account API
  wiring are next. [Evidence](evidence/m2/unity-history/README.md).
  Prior backend suite remains 113 passed checks; not rerun for this isolated adapter.

- **Repository publication (2026-10-05):** owner-created commit 7873ad0 pushed to
  origin/master, including 150 LFS objects (362 MB). GitHub accepted the push with a
  size warning for the 53.11 MB Alita CC_Base_Body.asset; no assets were dropped.

- **Synthetic history client (2026-10-05):** a session-only .NET client now hydrates durable
  history and follows SSE with applied-event checkpoints, duplicate suppression, bounded
  reconnect and cancellation. UTF-8/frame fragmentation and real API owner isolation are
  covered. **53 database/worker + 47 HTTP + 13 client checks passed**.
  [Evidence](evidence/m2/database/history-client.md). This is not yet a Unity-compatible
  transport or account UI; that integration remains the next step. No retention change.

- **Live durable SSE (2026-10-05):** the local synthetic account API now streams persisted
  admission/terminal events and resumes using Last-Event-ID. Bounded connections, owner
  checks, token-expiry closure and disconnect cleanup preserve ordinary API availability.
  **53 database/worker groups + 47 HTTP checks passed**; .NET build clean.
  [Evidence](evidence/m2/database/live-sse.md). M2 remains partial: Unity account/history
  client integration, production identity/providers and approved privacy policies remain.

- **Durable replay and cancellation (2026-10-05):** migration 007 adds transactional
  per-conversation event cursors. Synthetic HTTP routes expose bounded owner-scoped
  canonical messages and event replay; cancellation fences late worker replies and
  retains usage holds until trusted reconciliation. **53 database/worker groups + 38
  HTTP checks passed**, plus a clean .NET build. [Evidence](evidence/m2/database/replay-cancellation.md).
  Advances M2 durable conversation/replay/isolation foundations; live SSE and Unity account
  integration remain unimplemented. Real-user retention/providers/identity remain gated.


- **Bounded synthetic worker lifecycle (2026-10-05):** migration 006 provides owner-scoped
  claim/renew/finish with lease-token fencing. A real local one-pass worker claims queued
  work and commits a labeled synthetic reply; expired holders cannot complete after reclaim.
  **51 database/worker groups + 23 HTTP checks passed**. [Evidence](evidence/m2/database/worker-lifecycle.md).
  Provider execution/reconciliation, external worker identity, cancellation integration,
  persisted SSE/history and production policy remain pending. Unity remains session-only.


- **Atomic metered completion (2026-10-05):** migration 005 commits canonical reply,
  terminal event and usage settlement together through a worker-only database entry.
  Invalid usage rolls everything back; exact and simultaneous retries do not double-charge.
  **42 database groups + 23 HTTP checks passed**, including immediate-stop recovery.
  [Evidence](evidence/m2/database/metered-terminal.md). External worker authentication,
  generation dispatch, SSE/history and production identity/privacy remain pending.


- **Alita-only polish/performance (2026-10-05):** supersedes the three-character roster.
  Original and Cosmos removed from Unity Assets and active scenes; reversible imports/
  GUIDs retained under models/archived-unity, source exports unchanged. Alita-only header,
  refined portrait/materials/lighting, capped textures, exact derived facial meshes and
  batched blendshape updates. **123 mesh + 15 face/UI + 11 Replay + 11 transcription checks passed**. Thirty-second
  idle/speech Editor runs: p95 frame **5.702 / 4.379 ms**, zero frames above 100 ms.
  Referenced mesh/texture memory **23.5 / 27.8 MiB** (not process RSS). Full rig remains
  90,595 triangles / 24 material slots; mobile LODs/draw consolidation, device memory,
  thermal/30-minute soak and acoustic sync verification remain open. [Evidence](evidence/m1/alita-polish/README.md).


- **Swappable portrait characters (2026-10-04):** Original, Alita and Cosmos are selectable
  in TalkingCompanion with shared chat, speech, lips/expressions, reviewed transcription,
  Replay, Stop/Retry and New chat. **40 character + 11 transcription checks passed**;
  12 endpoint boundary and 8 readiness checks passed. All 150 prior metadata hashes and
  227 source-file hashes match. Portrait and Editor rectangles preserved.
  [Evidence/limitations](evidence/m1/character-roster/README.md). New rigs are Editor-ready
  prototype imports; mobile optimization and final materials remain pending. Installed
  qwen3:8b is selected locally because the previous qwen2.5:7b is no longer available.


- **Local account API (2026-10-04):** separate .NET loopback API with expiring synthetic
  identities, real non-owner PostgreSQL login and pooled owner isolation. Migration 004
  atomically binds admission and quota. **34 SQL groups + 23 HTTP checks passed**, including
  cross-owner denial, retries, cap rejection/rollback, expiry and restart. [Evidence](evidence/m2/database/account-api.md).
  Production mode is rejected; no OIDC/SSE/provider is enabled. The earlier .NET host block
  no longer reproduced in fresh M0 and account API builds/tests; no policy bypass used.

- **M2 quota primitives (2026-10-04):** configurable global/account caps, idempotent
  reservations and settlement with unused-unit release. **34 total database check groups
  passed**, including ten parallel requests, duplicate settlement, admission rollback and
  crash recovery. [Evidence](evidence/m2/database/quota.md). No production prices or free
  allowances selected. API integration and trusted usage reconciliation remain pending.
- **Publication:** prior prototype/database work pushed to origin/master as `6a2320d`.
  Quota was pushed as `9fef9cc`. New unintegrated `models/cosmos/` is preserved
  locally and excluded from the code push.

- **M2 terminal turns/outbox — implemented locally (2026-10-04):** migration 002 adds
  atomic completion/cancellation/failure, version checks, canonical replies and one
  database-local status consumer with dedupe. **23 total PostgreSQL check groups passed**,
  including terminal races, consumer-fault rollback, concurrent processing, Hindi/emoji
  storage and crash recovery of replies/dedupe. [Evidence](evidence/m2/database/terminal-outbox.md).
  This is not a running external worker, API integration or acoustic/Hindi voice evidence.

- **M2 database foundation — implemented locally:** transactional PostgreSQL migration
  for owner-scoped account conversations, accepted turns/messages, retry keys and
  pending outbox references. Forced RLS, composite ownership foreign keys, active-turn
  uniqueness and atomic idempotent admission advance **DATA-01, DATA-03 and ARCH-02**.
  **12 PostgreSQL 18.1 integration check groups passed**, including concurrent duplicate
  sessions and crash recovery. [Evidence](evidence/m2/database/verification.md).
- **Q-011 answered:** guest trial; account required for saved history and purchases.
  Guest limits/lifecycle/transfer consent remain open. No production identity or data
  policy was inferred. [Decision](requirements/QUESTIONS.md), ADR-038.
- **Project memory updated:** [MEMORY.md](MEMORY.md) records current capabilities,
  owner decisions, safe tool routing, outstanding gates and next steps. Root AGENTS.md
  directs future sessions to read and maintain it.
- **Next implementation:** atomic terminal settlement and trusted worker completion,
  then persisted events/history before approved external identity. Do not connect Unity to durable
  history until account boundaries, policy gates and API execution are verified.
- **Limits:** current Unity/Node conversation remains session-only. No public login,
  account linking, API-served durable history/SSE, production memory, billing, provider
  integration or deployment. M2 is partial and M1 physical/Hindi/provider gates remain.

## Latest verification: portrait and Device Simulator

- **Speech timing diagnostic (2026-10-04):** added explicit **Companion > Run Speech
  Timing Checks** and numeric-only CSV evidence. **11 real Editor checks passed**:
  multi-sentence playback/mouth motion, timing order, Replay isolation, New chat reset
  and cancellation. Two local trials reached first playback in **0.990 / 0.935 s**;
  maximum observed inter-clip waiting was **0.001 s** in each. These are main-thread
  observations, not acoustic latency, AV-offset or production percentile evidence.
  [Evidence](evidence/m1/speech-timing/verification.md). Advances PERF-01 instrumentation;
  physical measurements remain pending. Portrait and persistent window rectangles
  preserved; no UI rearrangement, audio recording, cloud integration or Android build.

- **Replay/context recovery (2026-10-04):** project-pinned Unity MCP now verified
  against `apps/unity`. Started the installed local Ollama engine; recurring unusable
  model output prompted schema-constrained text/emotion generation (ADR-036).
  **7 conversation-context + 11 Replay Editor checks passed** with real local AI/TTS.
  Earlier failures retained in replay evidence. Portrait 1170×2532, unchanged docked
  window rectangles, clean stopped scene and empty error/exception query verified.
  Asset/GUID and whitespace checks passed. No Android work attempted. Next: manual
  voice/lip comparison using Replay and English microphone quality evaluation; Hindi,
  physical devices and shipping provider gates remain open.

- **Local reply Replay (2026-10-03):** replay the last completed reply with identical
  audio, mouth cues and expression, without another AI/TTS request or context/transcript
  insertion. Stop preserves the original completed exchange; New chat/new prompt/scene
  exit releases the in-memory cache. **11 Editor checks passed**, including multi-sentence
  playback, facial motion, cancellation and portrait controls.
  [Evidence](evidence/m1/replay/verification.md). No recording files or mobile build.
  Next: use Replay for manual listening/lip comparison; physical audio and Hindi remain
  unverified and M1 device/provider gates are still open.

- **Actionable local failure recovery (2026-10-03):** distinct guidance for busy service,
  expired connection, timeout, unavailable AI/speech and unusable model output. Stream
  errors retain safe reason codes; unknown/raw errors are never displayed. Transcription
  recovery says record again/type instead, separate from chat Retry. **17 Editor error
  checks, 3 actual HTTP error checks, 12 service boundary checks and 19 real conversation
  checks passed**. [Evidence](evidence/m1/local-failures/verification.md). Actual long timeout
  and installed-speech outage remain unexecuted; those use injected Editor error frames.

- **Interrupted-turn context correction (2026-10-03):** commit user/assistant pairs only
  after all speech finishes. Stop/error no longer leaves an unfinished user prompt in
  the next request, and Retry avoids that duplicate. Four-exchange context bound removes
  whole pairs. **7 real Editor checks passed**, inspecting outgoing request history and
  exercising speech completion, interruption, Retry and rolling context.
  [Evidence](evidence/m1/conversation-context/verification.md). Session-only behavior;
  production persistence/retention and physical speech quality remain unverified.

- **Stable microphone picker (2026-10-03):** explicit choice is remembered locally;
  Refresh finds connected inputs without recording or switching an established selection.
  Missing inputs stay unavailable until reconnected or explicitly replaced. **12 selection/
  portrait checks + 11 voice-input regression checks passed** using simulated device lists
  and generated speech; no physical capture. [Evidence](evidence/m1/microphone-selection/verification.md).
  Asset preservation checks passed. Physical unplug/replug, permissions, actual voice
  quality and identically named device handling remain unverified. Next: owner chooses
  their intended microphone and repeats a previously misheard English phrase.

- **Local startup readiness (2026-10-03):** scene checks local service, Ollama and selected
  model availability; clickable setup status allows rechecking. Recognition is labeled
  configured, not audio-tested. **8 service checks passed** and actual Editor service-stop/
  restart checks preserved the draft. No automatic recording, inference or download.
  [Evidence](evidence/m1/local-readiness/verification.md). Physical microphone accuracy
  remains owner-unverified; M1 stays partial. Next: owner tests real dictation/voice quality;
  local failure recovery can continue independently of pending mobile/provider decisions.

- **English transcription improvement trial (2026-10-03):** installed pinned local
  Whisper-small with CPU/int8 and connected it to the existing review-before-send flow.
  Added quiet-input gain, speech detection and quiet/clipped/uncertain review guidance.
  **Five audio conditions passed** (clean, quiet, five-second lead, four-second internal
  pause/repetition, silence); **11 Editor input checks passed**. [Evidence](evidence/m1/transcription-quality/verification.md).
  Synthetic conversational comparison tied with Windows (one spelling edit in 38 words
  each); user's intermittent mishearing is not reproduced or confirmed fixed. No microphone
  was opened by tests. Next: owner repeats the problematic English phrase and checks the
  selected device; review state should say Whisper. Portrait/layout/assets preserved.

- **New local chat implemented (2026-10-03):** a header action cancels generation,
  transcription, recording and speech; clears the visible conversation, draft, Retry
  prompt and in-memory context; releases the prior clip/cues. **14 Editor checks passed**,
  including resets during real streamed playback, generated-audio transcription and
  generation, empty history in the next outgoing request and a successful fresh reply.
  Portrait view inspected; no layout/scene/GUID changes. [Evidence](evidence/m1/new-chat/verification.md).
  Initial inference check failed because Ollama was stopped; started the existing local
  engine and reran successfully. Physical recording/listening quality remains unverified.
  This is a local-session control, not production account deletion or retention policy.
  Next safe work: improve local service readiness/error explanations; owner can test
  microphone quality and sentence transitions. M1 device/provider gates remain open.

- **Incremental sentence speech implemented (2026-10-02):** the local talking scene
  shows completed text, then starts sentence audio while remaining audio is prepared.
  Bounded framing/queues, explicit completion and Stop flushing are verified. **8 new
  Editor + 11 service checks passed**, with 19 conversation, 11 voice-input, 10
  transcription and 12 service-boundary regression checks. Observed Editor first audio
  at 0.90 s versus stream completion at 1.33 s for a short two-sentence reply; this is
  one local observation, not a performance guarantee. [Evidence](evidence/m1/sentence-stream/verification.md).
  Model text still generates in full first. Portrait and Editor layout preserved;
  physical microphone/listening acceptance, Hindi and mobile remain unverified. Next:
  owner tests microphone and sentence-transition naturalness in the playable scene.

- **Local microphone input implemented (2026-10-02):** choose a device, explicitly Record,
  Finish & review, edit the recognized text, then Send. Local Windows English recognition;
  no automatic microphone opening or AI submission. Visible elapsed/level state, 20-second
  submission cap, Stop/discard, missing-device and no-speech/error handling. Raw recording
  stays in memory and uses the authenticated loopback endpoint. **11 new Editor + 10
  transcription checks passed**, plus **19 real conversation regression checks**, 12 existing
  service checks and the TypeScript/114 voice/19 loopback suite. Original asset/GUID checks
  passed. [Evidence](evidence/m1/voice-input/verification.md). Physical microphone capture,
  permissions, disconnection/noise and recognition quality remain unexecuted; tests used
  generated speech and silence. Portrait/layout preserved, no Android build. Next: owner
  tests their selected microphone; then improve voice-turn latency and recognition quality.

- Owner confirmed supplied asset rights and authorized public upload for another machine.
  Source models, imported FBX/textures, character screenshots and raw expression exports
  are now included, superseding the initial source-only publication. Binaries use Git LFS;
  original GUID metadata remains intact. README documents LFS checkout and local runtime
  prerequisites. Caches, builds and secrets stay excluded.
  Asset commit `43d2505` was pushed to `origin/master`: 164 unique LFS objects (443 MB).
  Fresh GitHub clone passed `git lfs fsck`; all 191 LFS files matched source bytes and
  all 137 Unity asset GUIDs matched. A fresh Unity import on another machine is unexecuted.

- **Lip-motion correction (2026-10-02):** replaced per-phoneme 25 ms opening/closing
  pulses with continuous blended poses and frame-rate-independent easing. Reduced lip
  strength and jaw travel; same mapped vowels no longer flutter closed. Speech remains
  on its original audio clock. **8 motion checks + 19 real Editor conversation checks
  passed**; preservation checks passed and final Console error/exception query was empty.
  [Evidence](evidence/m1/lip-smoothing/verification.md). Play Mode restored for owner
  comparison; portrait and window layout unchanged. Perceptual rig calibration remains open.

- **Owner priority: working Editor conversation before more Android builds.** Added
  `TalkingCompanion.unity`: real replies from installed local qwen2.5:7b, Windows Zira
  speech, sample-clock mouth cues, blinking/idle motion, relaxed arms and happy/concerned/
  curious/neutral expressions. Typed input, Stop, Retry and connection errors are wired.
  **19 real Editor checks + 12 service boundary checks passed**, plus actual offline-error
  and restart/Retry recovery with nonzero AudioSource output. Existing **114 voice + 19
  loopback checks**, TypeScript/client and repository preservation checks passed.
  [Evidence](evidence/m1/talking-companion/verification.md) · [Play instructions](runbooks/TALKING_COMPANION.md).
  Portrait 1170×2532 and Editor layout preserved. No Android build this turn.
  This is real local inference/speech, not scripted replies; English-only Windows voice,
  provisional lip sync, complete-response playback, no microphone or persistent memory.
  Next unblocked work: improve conversational voice/animation quality in this scene and
  add local microphone input. Shipping providers, Hindi speech and device acceptance remain open.

- Added a synthetic loopback playback HTTP endpoint with temporary per-account tokens,
  server-registered delivery bounds, input limits and retry handling. **19 actual HTTP
  checks + 114 offline voice checks passed**, plus TypeScript/client checks.
  [Evidence](evidence/m1/playback-http/verification.md). No live provider or production
  identity. Unity remains disconnected from HTTP; its portrait settings/layout are
  untouched. Next independent step: connect the Editor diagnostic report through an
  explicitly local test transport and exercise interruption/retry over this boundary.
  M1 remains partial: approved provider setup and physical Android/iOS evidence are blocked.

- Added a scoped local playback-receipt boundary: reports must match registered audio
  format, generation and delivered-sample limits. Exact retries reuse the receipt;
  conflicting retries and stale unfinished generations are rejected. **114 voice
  checks passed**, plus TypeScript/client and saved Unity-report integration checks.
  [Evidence](evidence/m1/playback-receipts/verification.md). Identities and delivery
  metadata remain trusted fixture inputs; this is not live authentication or persistence.

- CC lab now captures an epoch-tagged sample-clock report before audio reset and shows
  its observed duration in the portrait controls. **125 Editor checks** passed; **95
  backend voice checks** passed. An actual Editor report (8,192 / 24,000 samples per
  second, 341 ms conservatively rounded) was consumed by the backend tracker with a
  synthetic timing fixture. [Evidence](evidence/m1/unity-playback/verification.md).
  No live network bridge or actual speech. Portrait/layout preserved; latest APK
  predates this change and this revision's Android rebuild is unexecuted.

- Added conservative heard-response tracking (VOICE-03): rebuild context only from
  fully played aligned text segments, reject stale/regressing reports, preserve grapheme
  boundaries and discard the unheard draft at finalization. **22 new checks passed**,
  including English/Hindi interruption through the coordinator/worker/fake transport;
  **85 total voice checks** plus TypeScript/generated-client checks passed.
  [Evidence](evidence/m1/heard-response/verification.md). Alignment and playback clocks
  are synthetic; no real hearing, speech-quality or provider integration claim.

- Implemented local worker delivery and a fake transport around the session coordinator:
  stable action acknowledgements, bounded retries/timeouts, playback flushing, blocked
  recovery and deduplicated simulated settlement. **41 session + 22 worker checks pass**,
  along with TypeScript/generated-client checks. [Evidence and limits](evidence/m1/voice-worker/verification.md).
  This is volatile offline simulation, not process-crash recovery or real media/billing.
  Unity portrait scenes/layout and provider decisions remain unchanged.

- Added the **local backend voice-session simulator** (VOICE-02/03/04 preparation),
  covering duplicate admission/end, scope isolation, heartbeat/absolute expiry,
  interruption epochs, stale media and simulated cancellation/settlement actions.
  [Implementation and limits](../services/voice-agent/README.md). Offline checks and
  evidence: [voice-session verification](evidence/m1/voice-sessions/verification.md).
  No live worker, provider, billing, authentication or Unity transport integration;
  provider selection stays open. Unity scenes, portrait settings and layout untouched.

- Latest Android rebuild **passed**: blink correctives and jaw comparison controls are
  included in `artifacts/android-test/20260927-195903/output/Companion-CC-Test.apk`.
  **171.8 MB**, 0 errors / 998 warnings; packaged portrait/debug/app-ID/ARM64 checks passed.
  Snapshot provenance verifies 272 matching input files. First attempt failed during
  Package Manager IPC startup; isolated retry succeeded. [Evidence and warning triage](evidence/m1/android/build-20260927-195903/verification.md).
  Editor layout, original scene and asset hashes preserved. No APK execution/device test.

- Added session-only jaw-angle adjustment and a bone-only preview to isolate skeletal
  motion from facial morphs. **120 checks passed** in the existing 1170×2532 portrait
  Simulator, including actual geometry movement/reset, cancellation and invalid inputs.
  [Jaw comparison evidence](evidence/m1/jaw-comparison/verification.md). Original asset
  hashes/GUID checks passed; Editor layout preserved. Exported expression records are
  archived, but full coordinate/rest-space bone conversion remains unverified. The latest
  Android build above includes these controls; physical execution remains unverified.

- Implemented the export's two partial-blink corrective curves and a **Half blink**
  control. **108 checks passed** in the existing 1170×2532 portrait Simulator; before/after
  captures inspected. Build subset now retains **32 names / 135 exact frames**.
  [Verification and source rules](evidence/m1/blink-correction/verification.md).
  The historical 169.4 MB APK below predates this change; the latest build above includes it.
  Full CC constraints and multi-bone jaw conversion remain incomplete.

- Implemented a CC diagnostic mobile-build mesh subset (ADR-013). Baseline build log
  attributes 88.7% of uncompressed user asset bytes to meshes. All 30 lab controls are
  retained in temporary build copies; 129 facial frames and base geometry/UV/skin/topology
  compared exactly. The original source/editor rig is preserved. [Checks and scope](evidence/m1/mesh-subset/verification.md).
  Reduced build and package checks **passed**: APK **169.4 MB**, down from 563.5 MB
  (**69.94% smaller**); diagnostic size target met. [Build evidence](evidence/m1/android/build-20260926-192707/verification.md).
  Triangle/material counts are unchanged; production/device acceptance remains open.

- Added **Companion > Audit CC Mesh Deformation**, an Editor-only preview-scene audit:
  **90 probes across 30 native controls passed** finite-geometry/binding/reset checks.
  All controls moved geometry; some same-named shapes on auxiliary meshes did not.
  [Calibration baseline and remaining fidelity work](evidence/m1/cc-calibration/verification.md).
- [Voice cost/processing proposal](runbooks/M1_VOICE_COST_PROPOSAL.md) now includes dated
  rates, checked illustrative arithmetic and region/retention findings. It is not a
  provider selection or complete production cost model. Q-006 processing-scope clarification
  is pending; no services/accounts/spending enabled.

- Owner requested Editor-only testing for now; physical installation/testing is deferred.
- Android Punch Hole Center (1440×3088) and iOS Notch Device (1170×2532): **98 CC
  assertions passed per profile**. Portrait safe-area layouts and rendered facial poses
  inspected; Android Blink/reset clicked manually. [Evidence](evidence/m1/simulator/verification.md).
- Mobile startup orientation lock and build preprocessor enforce portrait. Automatic
  preview resizing removed from scene-opening menus. Recorded Editor window positions
  and sizes match before/after switching the existing Game pane to Simulator. Main
  project remains apps/unity, StandaloneWindows64, Unity 6000.5.9f1; no package upgrades.
- Owner-authorized Android build retry **passed** after freeing space: ARM64 IL2CPP,
  development APK, 0 errors, 998 warnings. Packaged portrait orientation, debug flag,
  test application ID and native architecture **passed**. [Build and package evidence](evidence/m1/android/build-20260926-184350/verification.md).
  This was the full-rig baseline (563.5 MB); the reduced build above supersedes its size
  result. Historical APK: artifacts/android-test/20260926-192707/output/Companion-CC-Test.apk. Physical
  execution remains deferred/unexecuted. The [earlier failed attempt](evidence/m1/android/build-verification.md)
  is retained as history. No source cleanup, package upgrade or device installation.
- Preservation check **passed**: all 36 original asset/package hashes, metadata/GUIDs,
  baseline secret-pattern/ignore checks. These are not a comprehensive security audit.
- [Voice options](runbooks/M1_VOICE_OPTIONS.md) and its linked cost/processing worksheet
  are proposals; deployment-specific totals and Q-006 approval remain outstanding.
  No provider chosen.
- M1 stays **partial**: native audio/rotation/keyboard, physical performance, real bilingual
  speech and calibrated lip sync remain unverified. Next safe work: review auxiliary-mesh
  fidelity and narrow provider deployments once Q-006 processing scope is answered. Physical tests resume only
  when the owner elects to resume them; no repeated USB request is needed.

**Portrait correction through M0: implemented and Editor-verified.** All 11 behavior checks
and 61 portrait layout assertions passed. The original landscape/text-only narrow fallback
is superseded by ADR-007. Overall M0 still retains the unrelated verification gaps below.
The Unity mock is runnable and its Play Mode checks passed. The current optional API
binary is blocked by Windows Application Control. Do not count M0 as fully passed until
that check and clean Unity-import reproduction are verified. Local development only.

## Implemented

The sections below preserve earlier M0/M1 snapshots. Newer progress/evidence above and
docs/MEMORY.md supersede outdated capability, asset-rights and tooling statements here;
historical check results are not current passes for changed code.

- Owner-authorized CC test scene: `Assets/Companion/Scenes/CCCharacterTest.unity` with
  the imported character, basic URP Lit materials/normal maps, portrait preview,
  29 native multi-mesh facial controls, four presets, sweep, optional prototype jaw
  assist, sample-clock synthetic tone/cues and interrupt/reset. No external service.
  Runtime component `CCCharacterLab`; non-development build scene guard included.
- CC Editor checks: **98 assertions passed**, including each binding/application/reset,
  multi-mesh blink, jaw reset, invalid input, portrait reachability, audio/cancel/end,
  simulated background and 20 session cycles. [Evidence](evidence/m1/cc-character-test/checks.txt)
  and [portrait capture](evidence/m1/cc-character-test/neutral.png). Neutral/blink/open
  renders visually inspected. This is native CC test control, not full canonical-52
  calibration. Bone assist uses a provisional local jaw rotation, not complete CC
  expression metadata conversion. Real speech, physical device performance and
  complete shading/rig fidelity remain unverified. Original 121 model files unchanged.

- AGENT-01/02/04, HUMAN-01/02: requirements inspected; Q-001..003 answered and recorded;
  technical decisions documented; no external provisioning or paid services.
- REPO-01, ENV-01 (M0 subset): monorepo boundaries, root instructions, Git initialization,
  excludes, environment template, pinned tooling, bootstrap/test commands and local CI definition.
- UNITY-01/02, AI-01/03 (mock subset): pure C# state machine, deterministic provider,
  separate UI Toolkit scene, primitive character, loading, streaming, cancel, fail/retry and replay.
- API-01/03, EVENT-01/02 (text subset): JSON schemas, generated C#/TS DTOs,
  loopback HTTP/SSE simulator, duplicate-message/action handling, cancellation/replay.
- Requirement 29 scaffold: resource and supplemental client/internal-event schemas,
  typed generated client facades for the four implemented mock routes; schema drift checks.
- Local identity/commerce/push/voice fixture ports: fake subject, no grants, no delivery,
  deterministic diagnostic tone. No production integrations or voice UI.
- Owner-requested portrait correction: portrait startup, width-scaled vertical UI,
  always-visible character, bottom composer/actions, separate demo-controls overlay,
  safe-area insets and keyboard compaction. Plans/Unity/product/test requirements updated.

## Verification evidence so far

Environment: Windows, Unity 6000.5.9f1, .NET SDK 10.0.301, Python 3.14.2; no Git commit yet.

| Check | Status | Evidence |
|---|---|---|
| C# domain scenarios | passed | 13 groups via dotnet run --project tests/contract/Companion.Checks.csproj; includes 6 new facial/voice groups |
| JSON Schema/format/negative/Unicode/wire cases | passed | tools/check-schemas.py; 47 generated events |
| HTTP mock admission/SSE/replay/cancel/production rejection | passed earlier revision; current binary blocked | Earlier test output passed both groups; after adding generated resource/client types the rebuilt DLL was rejected by Windows Application Control (0x800711C7) before startup. Not a current pass. |
| Generated C#/TS clients and OpenAPI references | passed | C# builds, npm run check, tools/check-schemas.py |
| Supplemental schemas | passed | voice/viseme/internal-event positive/negative fixtures and meta-schema validation |
| Existing assets/packages preserved | passed | 36 original SHA-256 matches; tools/check-repository.py |
| Metadata/GUID and basic secret-pattern checks | passed | tools/check-repository.py |
| Unity scene creation and script compilation | passed | live MCP command in existing Editor, separate scene created |
| Unity Play Mode and visual inspection | passed | [11 checks](evidence/unity-smoke.txt), [rendered prototype](evidence/m0-complete.png); existing Editor via MCP |
| Portrait layout regression | passed | [61 assertions](evidence/portrait/layout-checks.txt): 360×640, 390×844, 1080×1920, simulated safe areas/keyboard, large text/long messages; screenshots visually reviewed |
| Portrait settings and GUID preservation | passed | Portrait-only PlayerSettings, 390×844 width-matched PanelSettings; all 48 prior M0 metadata/scene/package entries still match snapshot hashes |
| Cache-free source bootstrap | passed earlier M0 (tooling subset); not repeated for portrait UI | 192-file source export; clean venv/npm/.NET restore, domain/schema/TS/preservation checks. [Snapshot hashes](evidence/source-snapshot.json). No API-policy bypass attempted. |
| Fresh Unity Library import / physical player build | unexecuted | Existing Editor tested only. Open the source-only project on a clean Unity installation to validate package restoration; no second Editor/device build claimed. |
| Hosted CI | unexecuted | Workflow authored; no remote/actions run |
| Mobile IL2CPP, device lifecycle, accessibility, Hindi | blocked | M1 device/toolchain matrix and later accessibility work required |

Earlier generator scalar typing, missing RFC3339 validation, TypeScript parameter typing,
portrait layout and background progress issues were fixed and retested. No unresolved
application check failure is hidden as a pass. MCP discovery briefly unavailable during
imports/reloads; connection recovered. Latest Unity Console error query returned zero.

## Remaining work and limitations

**Host action required:** Windows/IT administrator must approve the trusted local API
build through the normal Application Control policy, or provide an approved development
environment. Do not disable/bypass protections. Rerun `./tools/test.ps1` afterward; the
script reports the API host block distinctly (exit 77). Until then current HTTP behavior
remains unverified. The Unity mock has no API dependency and is usable now.

Startup/testing instructions are in [README](../README.md). Source-only bootstrap checks
do not replace a fresh Unity import or mobile execution. The CI workflow exists but has
not run on a hosted service; no remote, commit, push, signing or release was created.
Resource/event schema scaffolds are not full future API implementations or ownership tests.
Typed client facades require an injected transport; Unity currently uses its in-process mock.
No durable database/auth, production policies, Hindi UI, real AI, speech, memory, commerce,
admin application, cloud or store integration. No complete security/license audit or
physical-device evidence. The requirement package's MANIFEST describes its original
distribution; QUESTIONS.md is now intentionally maintained and differs from that baseline.

## Next milestone and human gates

### M1 local diagnostic slice

**CC5 source inspection update:** owner supplied `models/femaleCC.Fbx` and textures.
[Assessment](evidence/m1/cc5/ASSESSMENT.md): valid Humanoid avatar, 348 body facial shapes,
eight actual mesh deformation probes and rendered blink/pucker poses verified. All
117 texture references accounted for (37 embedded images extracted). Source unchanged.
Now wired into a separate character test scene; the chat avatar is unchanged. Needs
CC channel/bone mapping, URP materials and optimization (252,192 triangles, 25 material
slots); full 52-channel/perceptual/device acceptance remains unverified. Q-010 content
source/license follow-up pending. AndroidPlayer module now present following Editor
restart; isolated build retry passed as recorded above; physical tests deferred. Earlier
"no Android module" / "no assets" rows below describe the preceding diagnostic run.

- FACE-01/02, AVATAR-02, VOICE-01/03 diagnostic subsets: separate portrait `FacialDiagnostics.unity`, procedural
  52-channel mesh, binding validator, 15 viseme cues and generated diagnostic tone.
  Manifest: `assets/manifests/synthetic-facial-lab.json`. This is not a production avatar.
- Pure C# normalized facial mixer and bounded sample-clock queue, utterance/epoch
  invalidation, stale/invalid/overlapping frame rejection, mock transport with no capture.
  The two interfaces are diagnostic scaffolds, not complete provider/transport contracts;
  permission/token/lease/transcript/usage/native routing behavior remains unimplemented.
- Unity controls for individual channels, sweep, tone playback and interruption;
  lifecycle/configuration callbacks flush state. Non-development build guard covers lab.
- Owner answers Q-004/Q-010 recorded: S23 Android 16 / One UI 8, no iPhone, owner installing
  Android tools, no production assets; synthetic diagnostics explicitly authorized.
- [Device matrix and repeatable tests](runbooks/M1.md),
  [English/Hindi manual cases](runbooks/M1_BILINGUAL_FIXTURES.md). Cases are unexecuted,
  not a claim of localization, Hindi shaping, or speech/safety quality.

| M1 check | Status | Evidence / limit |
|---|---|---|
| Core facial/voice checks | passed | 6 added groups; total 13 domain groups passed |
| Actual Unity mesh/audio mechanics | passed | [61 Editor checks](evidence/m1/synthetic-checks.txt); 52 baked mesh deformations, sample clock, interrupt/late frame, completion, simulated callbacks, 20 playback cycles |
| Portrait lab visual inspection | passed | [390×844 capture](evidence/m1/synthetic-lab.png); synthetic disclosure and controls visible |
| Generated contracts/schema/TS/build/preservation | passed | tools/test.ps1; 36 original asset/package hashes, GUIDs and secret-pattern heuristic pass; API builds without warnings/errors |
| Current optional API execution | blocked | Host Application Control still rejects DLL; no current HTTP pass |
| Native route/lifecycle, physical IL2CPP, memory/thermal/acoustic latency | deferred / unexecuted | Android APK built/inspected; owner requests Simulator interaction tests only; no iPhone; callbacks are not physical evidence |
| Production rig, calibrated speech/visemes, Hindi rendering/voice | blocked / unexecuted | CC asset authorized for local tests only; production/voice rights pending; no provider selected; bilingual manual cases prepared only |

No Editor/package/render pipeline change. Unity MCP connected to this project's existing
Editor; discovery briefly drops during compilation and recovers. New assets received
Unity-generated metadata. No credentials, provider SDK, network capture, accounts or spending.
M1 is not complete: the board/tone cannot satisfy the mobile and real-voice exit criteria.

Next unblocked task: provider capability/cost proposal and local rig calibration.
Actual SDK integration waits for Q-006
approval of vendor/region/terms/spend. Physical testing is owner-deferred and would require an authorized S23;
iOS evidence requires iPhone plus Mac/Xcode. Minimum supported devices remain undecided.

M1 independent work: prepare rig/viseme diagnostics, device test matrix, bilingual fixtures
and a provider capability/cost comparison. Q-004: owner supplies available Android/iPhone
models and minimum OS goals. Q-006: approve provider, region/data terms, limited spike budget
and scoped credentials through a secret manager (never chat). Q-010: provide original or
licensed rig/voice provenance. These gate real voice/rig/device exit evidence, not local
mock preparation. India/bilingual product scope does not authorize hosting or legal policy.

All later P0 requirements remain mapped to their milestones/owners in
requirements/26_DEFINITION_OF_DONE.md; this M0 subset is not production acceptance.
