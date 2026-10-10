# Implementation status

Updated: 2026-10-10. Active work: **M1 partial; independent M2 durable conversation foundations underway**. Playable local AI talking-character scene verified in Editor; mobile/production-provider evidence pending.
M0 remains implemented with partial / blocked verification as detailed below.

## Current release readiness — audited 2026-10-08

**Google Play public release: not ready.** [Current requirement-by-requirement audit
and ordered release backlog](PLAY_STORE_READINESS.md) is the authoritative current
summary; dated progress below records historical implementation evidence.

| Milestone | Current status | Remaining gate |
|---|---|---|
| M0 scaffold | Implemented locally; verification partial | Clean current checkout/Unity import and hosted CI proof |
| M1 mobile feasibility | Partial | Current app release build and physical Android testing; iOS remains separate product work |
| M2 persistent text | Partial local foundation | Real identity, durable normal-chat integration, approved privacy and memory |
| M3 production voice/character | Partial local experience | Shipping mobile providers/transport, moderation, bilingual/AV/device evidence |
| M4 wardrobe/commerce | Partial local preview | Authoritative inventory, asset delivery and real store billing |
| M5 hardening | Specifications/scaffolding; not release-verified | Deployed operations/admin, security, load, recovery and cost evidence |
| M6 beta | Not started/evidenced | Safe signed integrated build, accounts, approvals and tester feedback |
| M7 public launch | Blocked on preceding gates | Store/release checklist and explicit exact-build authorization |

Latest recorded Android artifact is the September 27 CCCharacterTest development APK
(171.8 MB), not the current Alita chat app or a release AAB. Source Android package ID
is still the Unity template; target SDK is automatic and packaged API level unverified.
Normal conversation uses Windows/local Ollama speech; durable history uses a separate
synthetic-account lab. These must become one authenticated mobile product.

The old Windows Application Control blocker no longer reproduced in October 4 builds;
supplied-asset rights were confirmed by the owner. Older statements below are retained
as history, not active blockers. Mobile testing remains owner-deferred. Current owner
UI direction is a transparent overlay with stable full-body framing (ADR-066), replacing
old separate-drawer/keyboard-zoom requirements; ADR-067 replaces text outlines.

Next safe work: mobile service/configuration boundary and durable normal-chat integration
with test adapters; prepare provider/identity/hosting decision package. Provider, budget,
privacy and store-owner decisions remain open. This audit did not run tests or build,
change the Editor, authorize deployment, or mark any launch checklist item passed.

## Current production-directed progress

- **Arjun's modular wardrobe (2026-10-10, owner request):**
  - **Modular characters:** Arjun is now a base body (skin, hair, face rig) plus six swappable
    garments on his skeleton, with `CompanionGarment` data and a Male `CompanionWardrobeProfile`.
  - **Outfits:** Signature look (his original outfit, visually unchanged) and Chambray casual from
    the owner's reference: open chambray shirt over a white tee, olive chinos, white sneakers and
    a steel watch. Mix & match per slot.
  - **Category rule:** garments of another category or body are never worn or offered; checked
    both ways.
  - **Physics:** each shirt owns its spring chains; chains of garments not worn are paused.
  - **Owner review fixes:**
    - Shirt colour on the neck: neck skin left inside the shirts is back on the base body.
    - Armpits stretching when the arms rise: half-rotation shoulder share bones driven by
      `CompanionSecondaryMotion.shares`, plus new underarm weights on both shirts.
    - Also tidied: the tee neckline, the collar's back edge and the right lapel end.
  - **Checks:** Arjun rig 132 (new: neck coverage, share joints, side panel vs chest) and in-app 32.
    Regressions pass: Meera rig 105 and in-app 27, Tara rig 106 and in-app 27, face performance 68,
    body idle 34.
  - **Evidence:** [README](evidence/m1/arjun/wardrobe/README.md), ADR-075.
  - **Open:** female garments; sleeves rolled higher than the reference; procedural textures; no
    on-demand loading and no device or performance evidence.

- **Arjun, first male companion (2026-10-09, owner request):**
  - **Source:** a third owner-supplied Tripo GLB (placeholder name "Arjun"), presented as an adult
    at 1.78 m.
  - **Rig:** built with the skill, including stage-12 face polish. 84k-triangle body; 92 deform
    bones including 10 short-hair and 8 shirt-hem spring chains; 76 face channels.
  - **New techniques:** watershed split of shoes and trousers; a ray test for fringe vs painted
    brows; neck and collar weighting so head turns and lowered arms stay clean; a smoother
    rounding envelope.
  - **Voice:** turns now carry a per-companion voice and name. The local service maps them to the
    Zira or David voices and tells the model the companion's name. Arjun speaks with David.
  - **Checks:** Arjun 106 rig, 27 in-app and 17 face checks; 16 service checks; a live turn and a
    29 s review video. Meera, Tara and Alita regressions pass.
  - **Evidence:** [README](evidence/m1/arjun/README.md), ADR-074.
  - **Open:** generated shapes; dark-shirt tints read darker; desktop TTS voice; four loaded rigs
    with no device or performance evidence; Tripo rights (Q-010); placeholder name.
  - **Editor state:** stopped, scene saved and clean, Alita default, owner pref (Tara) unchanged.

- **Talking face polish for Alita, Meera and Tara (2026-10-09, owner request):**
  - **Lip-sync:** rewritten (cue-duration timeline, lip/jaw/tongue coarticulation, guaranteed
    p/b/m and f/v contacts, loudness and emphasis, frame-rate-independent smoothing, latency
    lead).
  - **Face layer:** new `CompanionFace` handles emotion onset, linger and fade; Duchenne
    smile; brows; speech-paced blinks; thinking look-aside; head motion.
  - **Tuning:** per-character `FaceTuning`.
  - **Meera and Tara:** re-rigged in Blender (skill stage 12) with corner-aware jaw weights,
    lip-only visemes and ARKit mouth shapes, a fixed tongue and teeth, and a painted 256 px
    mouth atlas.
  - **Alita:** gets her CC cheek-raise/squint channels for genuine smiles.
  - **Checks:** all passed — 19 speech-motion, 50 face, Meera rig 105, Tara rig 106, in-app
    26 + 26, Alita meshes 143 and the other Alita regressions, real conversation 19, speech
    timing 11.
  - **Live turns:** first audio 0.64–0.69 s.
  - **Evidence:** [README](evidence/m1/face-performance/README.md) with a review video;
    ADR-073.
  - **Open:** generated (not sculpted) shapes with small oblique corner artifacts; SAPI-only
    viseme input; no acoustic AV-sync, device or performance evidence.
  - **Editor state:** stopped, scene saved and clean; Alita is the scene default and the
    owner's character pref (Tara) is unchanged.

- **Tara, third rigged character (2026-10-09, owner request):** second owner-supplied Tripo
  GLB (placeholder name "Tara") rigged in the live Blender session with the Meera pipeline:
  78k-triangle body, CC_Base skeleton with Blender IK controls, 17 spring chains (hair,
  earrings, tunic panels, tassel, sleeves), rotatable eyeballs, eyelid shells, cut lips with
  mouth interior, 76 face channels incl. all 52 FACE-01. Shared code generalized (CharacterSetup,
  CharacterRigChecks, suffix wardrobe roles, postprocessor for any rigged Imported/<Name>).
  105 rig checks, 26 in-app checks and three live local-AI speech turns on Tara passed; Meera
  104 + 26 and Alita regressions (123 mesh, 15 face, 34 body idle, 56 polish, 22 transparent
  chat, 14 New chat, 8 wardrobe UI, 24 skin-tone UI, 11 real speech replay) passed.
  [Evidence](evidence/m1/tara/README.md), ADR-072. Generated shapes, recoloured denim back,
  three loaded rigs without device/performance evidence and Tripo rights (Q-010) remain open.
  Editor stopped, scene saved and clean, Alita default, device character pref unchanged.

- **Meera, second rigged character (2026-10-08, owner request):** owner-supplied Tripo GLB
  rigged in the live Blender session (CC_Base skeleton, IK controls in Blender, hair/
  earring/kurti/sleeve chains, rotatable eyeballs, eyelid shells, cut lips with mouth
  interior, 10 visemes + app expressions + all 52 FACE-01 channels) and integrated beside
  Alita: Settings Companion picker, name-aware labels/greeting, role-aware wardrobe with
  per-character looks, new CompanionSecondaryMotion springs. 104 isolated rig checks,
  26 in-app checks and a live local-AI speech turn on Meera passed; Alita regressions
  passed (123 mesh, 15 face, 34 body idle, 56 polish, 22 transparent chat, 14 New chat,
  8 wardrobe UI, 24 skin-tone UI, 11 real speech replay). [Evidence](evidence/m1/meera/README.md),
  ADR-071. Generated (not sculpted) shapes, weaker Tripo back texture and no device/
  performance or commercial-rights evidence remain open. Editor stopped, scene clean.

- **P02 durable command adapter (2026-10-08):** stable client/idempotency IDs, admission
  reconciliation, explicit versioned cancellation and rejected-session gating implemented
  for the local synthetic account API. 16 Unity/API/PostgreSQL command checks passed;
  full harness passed 114 groups including the command-suite gate. [Evidence](evidence/production/p02/README.md),
  ADR-070. No normal-chat or mobile production integration yet; next P03. Pending command
  state is memory-only; restart persistence remains P09. Editor stayed stopped/unchanged.

- **Sequential production delivery / P01 (2026-10-08):** [45-subtask queue](PRODUCTION_BACKLOG.md)
  now tracks dependencies, external gates and completion evidence. First subtask verified
  locally: connection source/request boundary; development credentials excluded from
  players; missing configuration keeps drafts and avoids phantom sends/audio capture;
  bounded requests reject redirects. 18 contract, 14 boundary UI and 14 live streaming
  checks passed. [Evidence](evidence/production/p01/README.md), ADR-069. Live first-text
  observation 31.244s exceeds target; functional pass only. Next P02/P03 durable chat
  integration. No production or physical-device acceptance claimed.

- **Text rendering fix (2026-10-08):** removed the outline effect causing broken
  glyphs in transparent chat; retained soft shadows for contrast. UI Toolkit/TextCore,
  not a TMP component fault. Native 1170x2532 and 390x844 captures reviewed. ADR-067;
  [evidence](evidence/text-rendering/README.md). Physical-device readability unverified.

- **Transparent full-screen chat (2026-10-07):** owner-requested replacement of the
  separate chat drawer. Transparent messages/composer overlay the stable full-body scene
  below the top nav. Scrollback and latest-message controls retained; keyboard moves the
  chat without zooming the character. Expanded-chat controls removed. 22 overlay,
  56 portrait, 14 real streaming and 19 lifecycle checks passed (111 total). ADR-066;
  [evidence](evidence/transparent-chat/README.md). Physical readability/IME QA pending.

- **Progressive text/voice + AI activity (2026-10-07):** text now grows as the local
  model generates; sentence synthesis overlaps inference and voice starts before the
  entire stream completes. Local AI Online/Typing/Speaking/Last seen reflects service
  observations, with foreground refresh and session-only timestamps. Fixed stream-follow
  scrolling and transcript space. 12 decoder, 13 real service, 14 live UI and 11 replay
  checks passed. [Evidence](evidence/streaming-presence/README.md), ADR-065.
  Local Windows/Editor implementation; mobile transport/provider gates remain open.

- **Microphone permission recovery (2026-10-07):** UNITY-03 partial. Explicit explanation,
  Android/iOS request adapters, denial/settings fallback, fresh recording gesture after
  grant, cancellation of pending UI on pause/Back, capture permission rechecks and audio
  configuration interruption guidance. 16 permission, 20 lifecycle, 56 portrait and
  12 microphone-selection checks passed (104 total). Editor stopped, scene clean, Console clear.
  [Evidence](evidence/microphone-permission/README.md), ADR-064. Native builds/prompts,
  phone-call/Bluetooth audio focus and physical capture revocation remain unverified.

- **Mobile lifecycle and Back navigation (2026-10-07):** UNITY-02/UNITY-03 partial.
  Backgrounding stops local work and portrait rendering; resume preserves the in-memory
  draft and requires explicit actions to restart voice/network work. Contextual Back
  closes overlays, rolls back wardrobe preview and collapses chat. 20 lifecycle checks
  (including 20 pause/resume cycles), 56 portrait and 11 real local speech/replay checks passed.
  [Evidence](evidence/mobile-lifecycle/README.md), ADR-063. Next: native permission/audio
  focus and Android Back/keyboard validation; M1 physical-device gates remain open.

- **Skin-tone customization (2026-10-07):** six Style swatches update head, torso,
  arms and legs together with texture detail retained. Live preview, Save, Cancel and
  signature reset use the existing local appearance preference; old saves remain valid.
  13 material and 24 UI checks passed; face/body palette and portrait UI reviewed.
  [Evidence](evidence/skin-tones/README.md). ADR-062; mobile-device QA pending.

- **Varied expressive idle (2026-10-06):** small foot adjustments, hip turns, shoulder
  rolls, side stretches and a two-arm yawn layered over breathing and relaxed fingers.
  Varied scheduling excludes the previous two gestures; yawns/stretches have cooldowns.
  Support-foot IK, conversation interruption and reduced-motion behavior implemented.
  29 new motion/schedule checks + 34 baseline checks passed; 75 outfit/gesture/angle
  renders reviewed. Five live gesture/framing checks and 56 chat-layout checks passed;
  46.5-second actual app capture saved. 11 real speech/replay checks passed (135 total);
  Editor stopped, scene clean, portrait preserved, Console clear. See ADR-061; device QA pending.

- **Local wardrobe + attentive idle (2026-10-06):** Style previews two tops and two
  bottoms independently, plus the signature dress, cloth/hair/sneaker tints and camera
  turn control. Save persists locally; Cancel restores the prior complete look. Fitted
  garment sources saved from the existing interactive Blender instance. Gaze now leads
  with the eyes, holds room glances, and returns toward the user during conversation.
  112 checks passed (34 body, 3 outfit/gaze, 8 UI, 56 chat-layout, 11 speech/replay);
  four combinations reviewed at three angles/poses; 44-second live recording verified. [Evidence](evidence/wardrobe/README.md). Local WARD-01 preview only;
  production inventory/commerce, mobile budgets and other body shapes remain unverified.

- **Articulated idle hands (2026-10-06):** owner rejected the prior straight fingers.
  Added all 30 finger/thumb joints, progressive curl, thumb opposition, softer wrists,
  asymmetric arms and a common 24-second body/hand cycle. Revised thumb splay after
  front/side close-up review. **34 rig checks passed**, including digit motion and loop
  closure; live app motion verified. [Visual review and previews](evidence/hand-idle/README.md).
  This supersedes the hand quality of the first idle pass below; visual review is recorded
  separately from technical test results.

- **Relaxed full-body idle (2026-10-06):** replaced the static A-pose with procedural
  breathing, hip weight shifts, spine/shoulder/arm/wrist motion and soft elbows. Leg IK
  holds foot placement/orientation; face and speech remain independent. Added Reduce idle
  motion preference. **31 rig checks**, **56 portrait checks**, **11 real speech/replay
  checks**, live motion measurement and reduced-motion check passed. Runtime animation
  [preview/evidence](evidence/body-idle/README.md). Existing character, outfit, scene and
  GUIDs preserved. Advances AVATAR-02; physical mobile performance remains unverified.

- **Full-body glass UI (2026-10-06):** implemented warm-room background, adaptive full-body
  Alita, native glass chat drawer, message bubbles, multiline composer, vector mic/send
  controls, conditional Stop/Retry/Replay, recording level/timer and modal settings.
  Settings retains diagnostics/history, adds larger messages/reduced transparency and
  confirms New chat. Retry no longer duplicates user bubbles. **56 portrait/interaction,
  11 actual speech/replay, 11 actual transcription, 12 microphone-selection and 14 New chat checks
  passed**. [Runtime captures/evidence](evidence/ui-polish/README.md). Actual keyboard/IME,
  accessibility, Hindi and device render budgets remain unverified. Scene/assets preserved;
  this is the first production-directed UI slice, not launch completion.

- **Production UI design (2026-10-05):** owner selected warm evening room, emerald accents
  and smoky glass, with full-body Alita and floating messaging overlays. Created a
  [design/build plan](design/production-ui/PLAN.md), generated conversation/recording
  concept and clean room plate, and saved exact prompts/provenance. Plan covers adaptive
  framing, keyboard/large text, dictation review, real message states, navigation and QA.
  Images visually inspected; runtime UI/camera unchanged in this design pass. Next: build
  immersive shell and chat interactions, then verify portrait layouts and regressions.
  This is design evidence, not a production-readiness or device test result.

- **Virtualized history (2026-10-05):** variable-height recycled rows now keep initial
  and end-of-history bound cards at **32 or fewer for 1000 turns** in the portrait test.
  **15 scale/keyboard checks**, **16 populated runtime checks** and full database/API
  harness **114 groups passed**; **12 recovery checks passed**. Editor stopped, scene clean.
  Offscreen terminal updates, larger text, scroll and
  session clearing are covered. [Evidence](evidence/m2/history-virtualization/README.md).
  Canonical data remains capped at 1000; device frame/memory and accessibility QA pending.
  This supersedes the initial-construction limitation in the historical entry below.

- **History rendering and keyboard access (2026-10-05):** retained cards update by turn
  version; unchanged renders preserve row identity/scroll. Added focus outline and
  Home/End/Page Up/Page Down transcript navigation. **13 runtime scale/keyboard checks
  passed** with 1000 turns; **12 recovery + 8 actual-API checks** rerun; full harness **114
  groups passed**. Warm detached render: 71–125ms before, 0.13–0.16ms after (not frame time).
  [Evidence](evidence/m2/history-performance/README.md). Initial construction/virtualization,
  screen readers and physical-device budgets remain open. Editor stopped, scene clean.

- **Runtime history recovery (2026-10-05):** expired/denied sessions now require reload,
  while transient outages retain bounded reconnect and explicit Retry. Stop cannot bypass
  credential failure. **12 runtime fault checks passed**, recovery captures inspected;
  **10 transport + 8 actual-API Editor checks** rerun, full harness **114 groups passed**.
  [Evidence](evidence/m2/history-recovery/README.md). Runtime 401/503 are controlled socket
  simulations; real API clock-expiry is tested separately. Editor stopped, scene clean.

- **Populated runtime history (2026-10-05):** real PostgreSQL/API history now verified
  through the runtime route with six long completed turns. Added session-only Larger
  messages (16→24px). **16 runtime checks passed** at 360x640 with safe-area insets;
  normal, enlarged and bottom-of-history captures visually inspected. Full
  `--api --unity-runtime` harness: **114 passed groups**. [Evidence](evidence/m2/populated-history/README.md).
  Play restored to stopped; scene clean; Editor layout unchanged. OS font scaling,
  screen readers, physical devices and production identity remain pending.

- **Development runtime history navigation (2026-10-05):** TalkingCompanion now has an
  Editor/development-only History lab route, Back restoration, pause/disable cleanup and
  safe-area insets. Missing setup is visibly unavailable; normal chat stays session-only.
  **13 runtime layout/navigation checks passed** at 360x640 and 390x844; setup-state captures
  visually inspected. [Evidence](evidence/m2/history-navigation/README.md). Play restored
  to stopped; active scene clean; layout/Game-view selection untouched. Populated runtime
  screen, physical devices and signed-build configuration remain unverified.

- **Real API Unity history screen (2026-10-05):** reusable vertical UI Toolkit view and
  explicit Editor lab menu now connect to the disposable PostgreSQL account API. Loading,
  live, retry, Stop, cancelled text and account-switch isolation passed **8 Editor checks**;
  the transport suite passed **10 checks**. Full `--api --unity` harness passed **114 groups**
  including the Editor bridge. Fixed JsonUtility's null-to-empty terminal text mismatch.
  [Evidence](evidence/m2/unity-account-api/README.md). This is an opt-in Editor lab, not yet
  a mobile navigation route; visual/safe-area and runtime lifecycle verification remain.
  Talking scene, portrait configuration, Play state and Editor layout preserved.

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
