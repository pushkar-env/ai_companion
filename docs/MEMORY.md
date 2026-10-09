# Project handoff memory

Updated: 2026-10-09. Read with STATUS.md, DECISIONS.md and requirements/QUESTIONS.md.
This file records implementation context, not the companion's memory of real users.

## Product and owner constraints

- Adults-only, non-explicit companionship, no clinical claims; India, English and Hindi.
- Original stylized adult avatar direction; temporary neutral brand. Owner confirms
  rights to supplied CC5 assets and authorized their public repository upload.
- Preserve `apps/unity`, all asset GUIDs, portrait-only app orientation, Editor docking,
  window rectangles and Simulator selection. Do not run layout/preview helpers.
- Prioritize a playable Editor conversation. Physical testing and more Android builds
  remain owner-deferred. Galaxy S23 / Android 16 / One UI 8 available; no iPhone/Mac evidence.
- Earlier OpenAI/ElevenLabs request was withdrawn. No shipping AI/voice provider, cloud,
  identity service, production retention policy, prices or spending authorization exists.
- Continue independent local work. Never request secrets in chat or silently treat a
  proposed policy/vendor as accepted. Q-011 answered 2026-10-04: guest trial; account
  required for saved history and purchases. Guest limits/data lifecycle and transfer
  consent remain undecided. Keep durable account data separate from guest sessions.

## Talking face polish (2026-10-09, owner request)

**Request.** More accurate, realistic lip-sync and expressions while responding, mainly for
Meera and Tara, with Alita polished too. Done locally (ADR-073, evidence/m1/face-performance).

**Runtime.**
- `Core/SpeechMouthMotion` was rewritten:
  - cue-duration timeline;
  - lip/jaw/tongue dominance coarticulation;
  - p/b/m and f/v contacts;
  - per-clip RMS envelope;
  - 2 ms critically damped substeps;
  - one DSP buffer of latency compensation.
- New `Presentation/CompanionFace` handles everything above the mouth.
- `CharacterOption.face` (`FaceTuning`) sets each character's jaw degrees and gains.
  - Presets: `FaceTunings.Alita()` 9.5° and `FaceTunings.Tripo()` 11°.
  - `Companion/Characters/Apply Face Tuning` re-syncs the roster from code.

**Checks and tools.**
- `Run Speech Mouth Motion Checks` (19) and `Run Face Performance Checks` (50) use an isolated
  `FaceRig`.
- `Render Face Performance Review` makes a frame-exact video. It needs ignored
  artifacts/face-review fixtures and ffmpeg.
- `LiveFaceCapture.Run` captures faces from real Play turns.

**Rigs.**
- Meera and Tara went through the new skill stage `blender/12_face_polish.py`:
  - corner-aware jaw weights;
  - lip-only visemes and ARKit mouth shapes (`mouthClose` seals over the bone's gap);
  - tongue and teeth placement;
  - 256 px mouth atlas.
- The Unity import uses blendshape normals None (calculated ones flipped on the lip walls).
- `models/*_Rig.blend` are now rig-only. Earlier full working files: Meera in git 1252d9a,
  Tara under `D:/Blender/Companion_Character_Rig_20261008_Tara/`.
- Working files are in `D:/Blender/Companion_Face_Polish_20261009/` (meera -02, tara -01;
  the repo copies match). Blender later restarted (PID 16028, empty startup file).

**Alita.** `Add Alita Expression Channels` copied her exact CC `Cheek_Raise` and `Eye_Squint`
channels into five runtime meshes. Mesh checks went from 123 to 143. The body asset is
66.7 MB and not LFS.

**Regression runs.** They rewrite other evidence (meera/tara PNGs, ui-polish, alita-polish).
Snapshot before running and restore the unchanged-result files afterwards. The ui-polish
folder holds the P03 session's uncommitted version.

**Pref.** The owner's device pref `Companion.Character.v1` is now "Tara"; the checks restore
it.

**Open.**
- Sculpted shapes or artist cleanup of the corner artifacts.
- Non-SAPI TTS cue mapping.
- Acoustic AV-sync, device and performance evidence.
- Tara's real name.

Nothing committed.

## Tara third character (2026-10-09, owner request)

- Owner supplied a second Tripo GLB (no name given; "Tara" is a placeholder) and asked for the
  Meera treatment. Done locally (ADR-072, evidence/m1/tara): roster Alita (default), Meera, Tara.
  Live Blender PID 13356 → `models/tara/Tara_Rig.blend` → FBX → `Companion/Characters/Import
  Tara` → `Check Tara Rig` (105) / `Check Tara In App` (26); three live speech turns passed.
- Character code is now generic: `CharacterSetup.Import(CharacterSpec)`, `CharacterRigChecks`,
  per-character spec files (`MeeraCharacterSetup.Spec`, `TaraCharacterSetup.Spec`), wardrobe roles
  by slot suffix, `MeeraModelPostprocessor` folds frames for any Imported/<Name>/<Name>.fbx with
  a rig.json (class name kept to avoid reimports). A fourth character needs only a spec + menus.
- Model-specific lessons: fused hair/tunic separated by ray layer counting, not colour; beige
  jeans back recoloured; jaw field must be zeroed below the neck (lacing leak caught by the rig
  check); barefoot means no shoe role; Trim slot is never tinted.
- Device pref `Companion.Character.v1` was already `Meera` on this machine before the session
  (owner choice); checks restore it. Regression reruns: ui-polish evidence restored to the P03
  session's uncommitted version; unchanged-result screenshots reverted to HEAD.
- Open: generated shapes, three loaded rigs with no device/perf evidence, Tripo rights (Q-010),
  placeholder name. Editor ended stopped, scene saved/clean. Nothing committed.

## Meera second character (2026-10-08, owner request)

- Owner asked to rig a supplied Tripo GLB and add her to the app with all existing features.
  Done locally (ADR-071, evidence/m1/meera). Roster is now Alita (default) + Meera; Settings
  Companion picker persists `Companion.Character.v1`; Meera looks save under
  `Companion.Appearance.v1.Meera` (Alita keeps the old key). Pipeline: live Blender session
  (PID 13356 at the time) → `models/meera/Meera_Rig.blend` → FBX → `Companion/Characters/
  Import Meera` (stopped Editor, re-runnable; MeeraModelPostprocessor folds `__fNN` blink
  frames at import, sparse deltas) → `Check Meera Rig` / `Check Meera In App`. Never write a
  text runtime mesh into Assets (642 MB) or refresh meshes with CopySerialized (broke skinning).
- Rig reuses CC_Base names so body idle/IK, gaze, jaw (local Z negative opens) and viseme code
  run unchanged; CompanionSecondaryMotion adds springs for hair/earrings/kurti/sleeves.
  Play-mode checks that assume Alita now call SelectCharacter(0) first.
- Owner will add more characters this way. Reusable pipeline: project skill
  `.claude/skills/tripo-character-rig/` (SKILL.md stages + pitfalls, verbatim Meera Blender
  stage code, `reference/unity-integration.md`). The next character first needs the
  Meera-named editor setup, postprocessor and wardrobe roles generalized (listed there).
- Open: generated (not sculpted) shapes, Tripo back texture, sleeve lining, no device/perf
  evidence, commercial rights unconfirmed (Q-010). Editor ended stopped, scene clean.

## Active sequential delivery (2026-10-08)

- P02 verified-local (ADR-070): SyntheticConversationCommands is the write-side adapter
  beside SyntheticHistoryTransport. Scope immutable per credential/conversation; Prepare
  retains text/client ID/idempotency key, Submit reconciles canonical GET, Cancel reads
  version then reconciles after mutation, Stop never asserts server cancellation. 401/403
  locks same-session retries. Pending state is memory-only and synthetic/Editor-only.
- 16 actual UnityWebRequest/API/PostgreSQL command checks passed; enclosing 114-group
  harness --api --unity-commands includes that suite's gate. Final run-miswefua; fixture
  removed, DB stopped, Editor stayed stopped, Console errors empty. Evidence production/p02.
  Existing tests/e2e/check_account_api.py now supports --unity-commands; at READY invoke
  ConversationCommandChecks.Run in stopped Editor. Never print fixture credentials.
- Next is P03, explicit synthetic chat UI integration using P02's composition contract
  (production/p02/README.md). Normal local AI chat remains unchanged/session-only. Do not
  merge streams or imply synthetic durable replies are real AI. P05/P07 later integrate
  production identity/provider execution; P09 handles persisted pending IDs/process death.
  Prior “Next P02” notes below are historical.

- Owner requests all pending work divided into subtasks and implemented sequentially
  toward production, updating progress/memory on completion. PRODUCTION_BACKLOG.md is
  the 45-item queue with dependencies and explicit external gates. No new paid-provider,
  physical-test or publication approval inferred. Do not spawn parallel agents by default.
- P01 verified-local (ADR-069): ConversationConnection pure immutable snapshot validates
  canonical loopback origins/hex tokens, rejects player local configuration and invalid
  routes. ConversationRequests owns bounded requests with redirects disabled; Editor
  session reader is #if UNITY_EDITOR_WIN. TalkingCharacter.Services owns source/snapshot.
  Submit/Retry/mic preflight missing config before changing draft/bubbles/capture.
  Players have honest unconfigured guidance; no production adapter exists yet.
- 18 contract + 14 runtime boundary + 14 actual streaming checks passed, evidence under
  production/p01. Live first text 31.244s exceeded target; P37 latency investigation,
  not a performance pass. Initial/final Play stopped, scene clean, layout preserved.
- Global .NET changed to 10.0.401 and could not satisfy pinned 10.0.301. Exact SDK installed
  locally under ignored artifacts/tooling/dotnet. Use its dotnet.exe for pinned checks;
  global.json unchanged. Source compilation/Editor tests passed; no mobile build.
- Next task P02: service/session composition and durable command adapter; existing API
  admissions route uses stable client_message_id and Idempotency-Key. P03 integrates normal
  chat afterward. Preserve explicit synthetic mode and account scope; no invented hosting,
  identity or approved real-user retention. Update queue/STATUS/MEMORY after each completion.

## Current release audit (2026-10-08)

- Owner requested a requirements/progress review through Google Play release.
  docs/PLAY_STORE_READINESS.md now maps requirement groups to implemented subsets,
  evidence, missing gates and ordered delivery. STATUS has a current milestone table.
  Public launch is not ready: normal local chat and synthetic account history remain
  separate; no shipping identity/AI/voice, safety/privacy system, memory, billing,
  operational admin or verified current-app release AAB/device evidence.
- Latest Android evidence is Sep 27 CCCharacterTest debug APK (171.8 MB), not current
  TalkingCompanion. Source package ID is Unity template, target SDK automatic. Official
  Play rules checked Oct 8: API 36 for new phone apps, 16 KB native compatibility,
  AI reporting, account deletion and conditional 12-tester/14-day closed testing.
- Audit only: no new test/build/Editor run or release authorization. Android-focused
  review does not remove iOS, Hindi, memory, realtime voice or commerce from P0.
  Optional reminders remain P1. Physical testing remains owner-deferred.
- Next safe slice: mobile service/configuration boundary + durable normal-chat wiring
  behind test adapters; prepare provider/identity/hosting/retention/budget decisions.
  Historical progress entries below are implementation snapshots, not current gaps.

## Working implementation

- ADR-067 fixes broken chat glyphs from ADR-066 outlines. UI Toolkit/TextCore uses
  SDFAA fonts; no TMP components/font atlas changes required. Drawer outline is zero,
  replaced with a soft text shadow. Same-resolution outline A/B and saved final renders
  at 1170x2532 and 390x844 visually reviewed (evidence/text-rendering). Prior temporary
  scheduled capture callbacks errored after their RenderTexture was destroyed; they
  were gone on resumption. Final capture uses separate setup/capture/restore calls.
  Editor initial/final on Oct 8 stopped, original MockPanel restored, no capture target.
  Physical-device readability remains unverified.

- ADR-066 supersedes split avatar/chat layout: stage is a root sibling behind the
  shell; chat covers below top nav with no drawer/bubble/composer fill by default.
  Camera depends only on scene viewport + safe insets, never chat/keyboard/wardrobe.
  Removed expanded/expandChat and obsolete Back-collapse behavior. Geometry changes
  follow latest only when followConversation=true; intentional scrollback preserved.
  Reduce transparency keeps optional contrast surfaces. New TransparentChatChecks
  verifies camera stability, transparency, full overlay, scrolling and keyboard bounds.
  Old ConversationPolishChecks stage assertion now expects overlap per owner direction.
  22 overlay + 56 portrait + 14 live streaming + 19 lifecycle checks passed (111 total);
  keyboard follows latest after resolved viewport layout (gap zero). Captures reviewed
  in evidence/transparent-chat. Initial/final Play=False, clean scene, portrait preserved.

- ADR-065 streams Ollama structured JSON through streamed-reply.mjs (bounded decoder,
  ordered delta frames, canonical text frame, concurrent inference/TTS capped at three
  speech jobs). LocalSpeechStream queue now holds bounded delta bursts. Unity grows one
  bubble and accepts audio before canonical completion; final text must match deltas.
  Presence shows Local AI Online/Typing/Speaking/Last seen based on real service state,
  30s foreground idle health checks, 45s freshness, session-only last availability.
  Scroll following tracks user input; expanded active transcript fixes spurious New message.
  12 decoder + 13 actual service + 14 live UI + 11 Replay checks passed; source service
  restarted using its verified session PID without printing credentials. Evidence under
  streaming-presence. 56 portrait + 20 lifecycle regressions also passed (126 total).
  ADR-065 permits foreground health probes after resume, not automatic turns/voice.
  Mobile/provider/safety streaming integration remains unverified. Initial/final
  Play=False; TalkingCompanion clean (five roots), portrait preserved, Console errors
  empty. Existing FindFirstObjectByType deprecation warning is unrelated.

- ADR-064 adds MicrophonePermission + TalkingCharacter.Permissions: explain → OS prompt
  → fresh mic tap; denial/settings/Not now; pending request cancellation on Back/pause/
  disable; permission recheck at capture/while recording. Desktop behavior retained.
  Android generated-manifest hook suppresses automatic prompts; iOS purpose configured.
  16 fake-adapter UI/build-transform checks passed, compact captures reviewed. Native
  branches/build/device permission and full audio-focus routing remain unverified.
  Audio configuration interruption now gives recovery guidance. No auto recording.
  Final regressions: 20 lifecycle + 56 portrait + 12 mic selection passed (104 total
  including permission tests). Initial/final Play=False, clean scene, Console empty.
  iOS purpose persisted in ProjectSettings. No native mobile build run.

- ADR-063: TalkingCharacter.Lifecycle.cs centralizes idempotent OS pause/resume,
  suspends portrait camera/body updates, aborts reply/setup work and preserves draft.
  Resume never auto-restarts voice/history/network. Mobile no longer forces background
  execution. UI Toolkit NavigationCancel routes Back through history/settings/wardrobe/
  recording/expanded chat; root leaves host handling intact. 20 lifecycle checks and
  56 portrait regressions passed; native Back/IME, audio focus and devices unverified.
  11 real speech/replay checks passed after restarting installed Ollama (qwen3:8b).
  Initial/final Editor Play=False; scene clean, portrait preserved. Draft remains memory-only.

- Skin tone is now part of CompanionWardrobe.Look (ADR-062). Six swatches at the
  top of Style, runtime-only matching skin material clones, preview/Save/Cancel/reset.
  Original preserves authored colors; old saves default to index 0. Tests/evidence in
  skin-tones: 13 material + 24 live UI checks. Mobile validation remains unexecuted.
  Final state: original appearance preferences restored, Play enabled as on entry;
  TalkingCompanion clean (five roots), portrait preserved, Console clear.

- Unity 6000.5.9f1; URP; startup prototype `Assets/Companion/Scenes/TalkingCompanion.unity`.
- Local English development adapter: Node service in `services/voice-agent`, installed
  Ollama (currently installed qwen3:8b), Windows Zira TTS, audio-clock visemes and expressions.
  Ignored artifacts/talking-character/model.txt selects this machine's model; environment
  COMPANION_LOCAL_MODEL overrides it. The repository fallback remains qwen2.5:7b. Local
  requests disable thinking. No shipping provider selected.
- Reviewed microphone drafts use locally installed Whisper-small when present; fallback
  Windows recognition is labeled. Accuracy on the owner's real voice remains unverified.
- Owner now requests Alita only (2026-10-05 continuation). TalkingCompanion and
  CCCharacterTest both use Alita. Original/Cosmos Unity imports, original diagnostic
  materials and former roster tools were moved with their GUIDs to models/archived-unity,
  outside Assets. Source exports remain unchanged. No character picker for a single rig.
- Alita has derived runtime mesh assets retaining exact geometry/skinning and only used
  facial channels; original FBX is intact. 123 mesh, 15 face/UI, 11 Replay and 11 transcription checks passed.
  Facial updates batch targets and skip unchanged weights. 4x portrait RT antialiasing,
  adjusted hair/skin/eye materials, warm/cool lighting, closer framing and dark input fields.
- Desktop Editor performance comparison completed: 30 s idle + 30 s cached speech each.
  Optimized p95 frame 5.702 / 4.379 ms; no >100 ms frames. Referenced mesh/texture sizes
  23.5 / 27.8 MiB. These are asset-reference sizes, NOT app/OS resident memory. Full rig
  is still 90,595 triangles / 24 material slots; mobile LOD/draw budgets remain open.
  Evidence: docs/evidence/m1/alita-polish. Portrait and Editor layout must stay preserved.
- Sentence speech, Stop/Retry, New chat, setup check, remembered microphone selection,
  Replay and timing diagnostic are implemented. Completed context holds four exchanges.
- Local structured model output requires text/emotion. Recent verification: 7 context,
  11 replay and 11 speech timing checks passed. These do not prove acoustic AV accuracy.
- The .NET execution block did not reproduce on 2026-10-04: fresh M0 API build and HTTP
  tests passed without security-policy changes. M0 and Node/Unity remain separate.
- services/account-api is a separate local .NET API using locked Npgsql 10.0.3, temporary
  synthetic-account tokens, non-owner PostgreSQL login and transaction-local RLS context.
  /local/v1 admission/turn lookup passed 23 real HTTP checks; no production OIDC/SSE.

## Current production-directed work

- ADR-061 adds CompanionBodyIdle.Gestures.cs: session-local randomized foot adjustment,
  hip turn, shoulder roll, side stretch and two-hand yawn. Avoids previous two gestures;
  side/strength/tempo vary, yawns cooldown80s/stretches35s. Runtime calls Advance; Sample
  remains deterministic baseline for original rig tests. SampleGesture is art-review path.
  Conversation suppresses new gestures and fades large poses; feet finish safe landing.
  Yawn face yields to speech; Reduce motion freezes relaxed body but keeps blinking.
  Camera caches raised-hand envelope once. 29 new + 34 baseline checks passed; 75 baked
  outfit/gesture/angle captures reviewed. Five live gestures/framing checks and 56 chat-layout checks passed;
  timestamped 46.5-second capture saved in docs/evidence/expressive-idle. 11 real speech/replay checks passed (135 total). Initial/final Play=False; scene clean, five roots, portrait preserved, Console clear.

- ADR-060 adds CompanionWardrobe, TalkingCharacter.Wardrobe, CompanionGaze. Style opens
  an in-scene try-on drawer: 2 tops × 2 bottoms plus original dress; per-slot/hair/shoe
  tints, preview orbit, transactional Save/Cancel, versioned local PlayerPrefs only.
  Blender live PID 55604 used; editable source models/wardrobe/AlitaWardrobe.blend and
  four JSON garment exports. tools/export-wardrobe.py runs inside that live session;
  Editor WardrobeImport creates/updates Resources/Wardrobe assets without new GUIDs.
  Source FBX/scene untouched. Gaze uses eyes-first held glances and conversation attention;
  body/finger idle unchanged. Review must BAKE sampled skin before rapid Camera.Render:
  unbaked repeated captures gave misleading stale skinning artifacts. Actual Play also
  reviewed. 34 body + 3 wardrobe/gaze + 8 UI checks green. 44-second actual runtime capture completed; live body motion verified. 56 chat-layout and 11 actual speech/replay regressions passed. Ollama was stopped;
  restored existing installed local server (qwen3:8b), then speech checks passed. Started and finished with Play=False.
  WARD-01 remains local preview only: no entitlement/commerce/remote catalog claim.

- Owner rejected straight palms/fingers in the first idle. ADR-059 adds 30 articulated
  finger/thumb joints with mirrored anatomical curl axes, thumb opposition, softer wrists,
  asymmetric arms and a coordinated 24-second cycle. First thumb pass was too splayed;
  refined after close-up inspection. docs/evidence/hand-idle holds front/side phase reviews,
  full-cycle diagnostic hand motion and a live app preview. BodyIdleChecks now verifies
  all 30 digits bend/animate and the complete pose loops without a jump (34 checks total).
  11 real speech/replay checks passed; Console clear, scene clean. Owner was in Play at
  the start, so final state is Play running. Review video hand-motion.mp4 is diagnostic
  pose sampling; app-idle.mp4 is live runtime capture. No scene/Editor layout changes.

- Owner requested full-body idle (2026-10-06), replacing the fixed A-pose. New
  CompanionBodyIdle drives 16 body joints through breathing/weight shifts and relaxed
  arms, with analytic leg IK keeping feet planted. No Animator/body clip existed.
  TalkingCharacter owns it; cached original pose restores on disable/switch. Face/jaw
  remain owned by speech; no source rig/scene changes. Settings Reduce idle motion
  holds the relaxed pose and suppresses head sway. ADR-058; evidence/body-idle.
  31 isolated rig checks, 56 portrait checks and 11 real speech/replay checks passed;
  actual Update motion and reduced-motion freeze verified. Recorded idle.mp4 from
  offscreen Unity frames. Stationary procedural idle; future expressive gestures or
  other outfits need separate art/rig review. No physical-device performance claim.

- Latest owner priority (2026-10-05): plan polished real-user UI with full-body Alita,
  glass overlays and WhatsApp-like chat/mic controls. Owner selected warm evening room,
  emerald accents, smoky glass. docs/design/production-ui/PLAN.md is the detailed plan;
  assets/ contains generated concept and clean room plate; PROMPTS.md records provenance.
  Owner authorized implementation: TalkingCharacter.View.cs + Resources/CompanionUI now
  provide full-body transparent rendering over the imported room, native glass drawer,
  vector icons, conditional Stop/Retry/Replay, multiline composer and modal settings.
  Full-body bounds bake once from posed geometry; camera restores on disable. Existing
  Alita/outfit/GUIDs preserved. Settings holds microphone/setup/history lab and confirmed
  New chat; blocks underlying input. 150% message text/reduced transparency are preferences.
  Mic remains record/review/send; no real call or human read-receipt implication. ADR-057.
  See evidence/ui-polish for captures/checks. Next: device keyboard/IME/accessibility,
  rendering budgets, production navigation and approved identity/provider integration.
  Verified 56 portrait/interaction checks, 11 real speech/replay, 11 real local transcription
  and 12 microphone-selection plus 14 New chat checks. Replay test waits for conditional action layout;
  settings-based tests open the panel before measuring controls. No auto microphone capture.

- M1 remains partial. Advancing independent M2 DATA-01/DATA-03 storage foundations with
  synthetic data while device/provider gates remain open.
- `services/api/Database/001_conversations.sql`: PostgreSQL owner isolation, composite
  ownership FKs, one-active-turn constraint and atomic idempotent text admission/outbox.
- `tools/check-database.py`: isolated local PostgreSQL 18 test cluster, random loopback
  port/password, synthetic test users, stopped artifacts; never uses an existing database.
- `002_terminal_outbox.sql` adds atomic completed/cancelled/failed transitions, canonical
  assistant text, expected-version/conflict handling and a database-local status consumer
  with transactional dedupe/projection/acknowledgement. No broker/provider daemon yet.
- `003_quota.sql` adds configurable global/account caps and idempotent reservation/
  settlement. No production allowance/rate is seeded. Expired holds await trusted usage
  reconciliation; no automatic release worker or provider integration exists yet.
- 34 actual PostgreSQL 18.1 groups passed: admission/RLS, terminal races, consumer rollback,
  Unicode, quota concurrency/settlement and crash recovery of replies/dedupe/balances.
  `python tools/check-database.py` reproduces them; clusters are stopped afterward.
- `004_metered_admission.sql` atomically binds turn/outbox/retry key/quota hold; duplicate
  client-message retries reuse the hold. Build services/account-api, then run
  `python tools/check-database.py --api` (34 SQL groups + 23 HTTP checks).
- 005_metered_terminal.sql adds finish_metered_text: atomic reply/outbox/settlement,
  restricted entry for NOLOGIN companion_worker inheriting trusted companion_runtime.
  Actual usage is a trusted-worker input; no client completion route. Existing low-level
  runtime privileges remain trusted SQL internals, not complete privilege separation.
  python tools/check-database.py --api now passes 42 SQL groups + 23 HTTP checks, including
  parallel retries and crash recovery. See evidence/m2/database/metered-terminal.md.
- 006_worker_leases.sql adds claim_local_turn/renew_local_turn/finish_local_turn, with
  worker-only owner-scoped leases, expiry, replacement tokens and atomic completed receipts.
  services/worker/local_synthetic.py performs one bounded pass using installed psql and a
  non-owner login; fixed labeled reply, zero provider units, no polling or AI calls.
  51 database/worker groups + 23 HTTP checks passed. Synthetic expiry recovery is NOT safe
  authorization to retry uncertain paid provider work; external reconciliation remains open.
- 007_event_replay.sql adds atomic per-conversation event cursors on the existing outbox.
  Account API exposes GET conversations/{id}/events and /messages (bounded JSON pages,
  not SSE), plus POST turns/{id}/cancel with expected_version. Owner-scoped read snapshots,
  canonical history, exact cancel retry, and late claimed-worker rejection are verified.
  Holds remain reserved until trusted settlement; expired cancelled leases need explicit
  reconciliation. 53 database/worker + 38 HTTP checks passed; evidence replay-cancellation.md.
  Unity account/history wiring and retention remain unchanged; Editor was not touched.
- EventStream.cs adds /local/v1/conversations/{id}/stream over persisted events.
  Last-Event-ID/after exclusive resume, 50-event batches, four global slots, 500ms poll,
  30-second lifetime bounded by local token expiry. Connections released between polls.
  53 database/worker + 47 HTTP checks passed (100 total); see live-sse.md and ADR-047.
  No Unity changes; next safe work is a synthetic account/history client with reconnect.
- packages/account-client is a .NET 10 synthetic client foundation, not Unity-compatible
  yet. It hydrates event history, projects canonical turn state and follows SSE using
  applied cursors, duplicate suppression and bounded reconnect/cancellation. Session-only,
  no credentials/cursors written to disk. Tests/account-client runs socket faults plus
  actual account API hydration/isolation; check-database.py --api builds it automatically.
  53 database/worker + 47 HTTP + 13 client checks passed; history-client.md and ADR-048.
- Unity AccountHistory/AccountEventDecoder + SyntheticHistoryTransport implement engine-
  compatible synthetic history hydration/SSE. 9 real-socket checks passed via menu
  Companion/Run Synthetic Account History Checks in stopped Unity 6000.5.9f1. No scene,
  Play state, package or Editor layout change. Keep adapter explicit and memory-only.
  Actual account API wiring and portrait UI remain next; see ADR-049/unity-history evidence.
- SyntheticHistoryView now supplies reusable vertical history UI (loading/live/retry/Stop),
  hosted explicitly by Companion/Open Synthetic Account History. Never auto-open the window.
  tools/check-database.py --api --unity creates an ignored short-lived fixture; at READY,
  Companion/Run Real Local Account API Checks drives the view in stopped Editor against
  actual PostgreSQL/API, writes matching run-id evidence, and lets harness clean up.
  8 real-API UI + 10 transport Editor checks passed; full harness 114 groups passed.
  JsonUtility null string -> empty string is normalized for cancelled/failed event text.
  This is an Editor integration lab; runtime mobile history navigation/visual QA remain.
  See docs/runbooks/DATABASE.md for the five-minute fixture workflow. No real chat storage.
- TalkingCompanion has Editor/development-only History lab runtime navigation. Explicit
  opening interrupts speech/recording, hides chat controls and loads ignored fixture only
  in Editor. Back restores original elements; pause stops listening; disable disposes.
  Missing fixture shows setup guidance. No standalone build credential delivery yet.
  13 Play-mode offscreen layout/navigation checks passed at 360x640/390x844, 24px insets;
  setup captures visually inspected. Stopped mode and clean TalkingCompanion scene restored.
  Never call PortraitPreview helpers; new test uses temporary scene/RenderTexture instead.
- Populated runtime checks now use --api --unity-runtime, which seeds a separate six-turn
  long-reply conversation and writes runtimeConversation in the ignored fixture. At READY,
  enter Play and invoke Companion/Run Populated History Layout Checks. 16 runtime checks
  passed (360x640, insets, scroll-to-last, pause/Back) plus full harness 114 groups. Captures
  normal/large/bottom visually inspected. Play restored to stopped; scene clean.
  Larger messages toggle sets body 16→24px, session-only; not OS-wide font/accessibility.
  Result fixture cleanup verified. See ADR-052/evidence/m2/populated-history.
- Runtime recovery now gates same-credential Retry on 401/403 and nonretryable errors;
  reconfigure/reopen with fresh session to recover. Stop preserves auth-error guidance.
  Live is shown only for 200 streams. Transient failure keeps bounded retry/manual recovery.
  12 Play-mode real-socket fault checks passed (partial frame, duplicate replay, simulated
  401, replacement session, four 503s); expiry/outage captures inspected. 10 transport,
  8 real-API Editor checks and 114-group harness rerun green. See ADR-053/history-recovery.
  Stopped Play state and clean original scene restored; all temporary fixtures removed.
- History cards are retained by turn ID/version; reflow updates sizes, account switch
  clears row map, diagnostic aggregate text built only on request. Transcript keyboard
  Home/End/Page Up/Page Down and focus ring added. 13 scale checks with 1000 turns passed;
  12 recovery + 8 actual-API checks and full 114-group harness rerun green. Warm detached
  renders improved 71–125ms to 0.13–0.16ms; not frame/device or allocation evidence.
  Before/after/checks/capture in history-performance; ADR-054. Initial card creation still
  unvirtualized. Unity allocation counter returned zero, treated unsupported/unverified.
- Initial history is now virtualized with dynamic-height ListView rows (ADR-055).
  15 scale/keyboard checks passed at 1000 turns with <=32 bound cards initially/at end;
  16 populated runtime checks and 114-group database/API harness passed (run-a3c95u1c).
  Account changes clear data/bindings; offscreen updates appear when scrolled into view.
  12 recovery checks also passed; Editor stopped, scene clean, Console errors empty.
  Evidence: history-virtualization. Historical retained-row benchmarks are not timings
  for this implementation; frame/RSS/device and accessibility validation remain open.
- Remaining backend work: device/accessibility QA,
  external provider dispatch/identity, production OIDC, privacy and memory policy.
  Do not connect real-user storage or silently change session-only prototype retention.

## Tooling and verification cautions

- Use `unity_companion` MCP for this project; `unity_ramayanam` is a separate connection.
  Verify Application.dataPath before mutations. Never call an unpinned Unity connection.
- Preserve initial Play state. Check result artifact timestamps: test files update only
  on completion and an old PASS file is not evidence for a newly started run.
- Node turn service allows one active turn; serialize endpoint and Editor conversations.
- Never print ignored `artifacts/talking-character/session.json` (contains bearer token).
  Avoid raw Unity Editor.log; it can contain launch credentials. Use filtered MCP errors.
- Current branch master; origin pushkar-env/ai_companion. Owner authorized push; earlier
  prototype/database work was published as 6a2320d and quota as 9fef9cc on 2026-10-04.
  Owner-created 7873ad0 includes Alita/archive, account API and .NET client work and was
  pushed successfully on 2026-10-05 (150 LFS objects). Preserve archived GUIDs. GitHub
  warned about the 53.11 MB runtime body .asset, but accepted it. Consult Git for subsequent
  Unity adapter publication. No force push or history rewrite was performed.
- STATUS/evidence distinguish passed, blocked and unexecuted checks. Historical STATUS
  sections are prior snapshots and must not override this current handoff or newer evidence.
