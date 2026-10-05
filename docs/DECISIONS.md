# Technical decisions

## ADR-041 — Loopback account API and atomic metered admission (accepted 2026-10-04)

The historical .NET execution block did not reproduce: M0 API rebuilt and passed HTTP
checks without policy changes. Add a separate .NET 10 account development API, preserving
M0 and Unity adapters. Npgsql 10.0.3 is pinned with a NuGet lockfile; use pooled non-owner
connections, parameterized SQL and transaction-local actor context. References:
[package](https://www.nuget.org/packages/Npgsql/10.0.3),
[official usage](https://www.npgsql.org/doc/basic-usage.html).

Migration 004 binds each turn to one owner-matched reservation. Acceptance, retry keys,
outbox, quota hold and binding commit together. Denied quota rolls back accepted work;
same-client-message retries under new keys reuse the hold. Budget/units are server-configured,
never request fields. This local slice uses the configured budget end as hold deadline.

Development /local/v1 routes are separate from production OpenAPI and expose admission
and owner-scoped turn lookup only. Random short-lived fixture tokens on loopback provide
synthetic identity, not OIDC. Startup rejects production mode and unsafe DB configuration.
The API uses a non-owner LOGIN inheriting companion_runtime, not an admin connection.
Temporary credential files remain ignored and are removed after tests. 34 SQL groups and
23 actual HTTP checks passed. No SSE/provider worker, production identity or launch claim.
Next: atomic terminal settlement/trusted worker completion, then persisted events/history.

## ADR-040 — Configurable durable quota accounting (accepted 2026-10-04)

Migration 003 adds global/account budget periods, idempotent reservations and a runtime
append-only settlement ledger. No production cap, price or allowance is seeded. Reserve
under owner/global/account locks, counting held and spent units against both caps. Exact
retry keys bind budget, units and deadline. Settlement records actual units within the
hold once and releases unused units; zero releases all. Runtime cannot raise caps or
edit/delete ledger entries. Private accounting uses owner RLS; global caps are shared
server configuration. These privileges belong to a trusted backend, never public clients.

Expired holds remain funded until trusted reconciliation; timeout is not proof of zero
cost. Returning a terminal reservation on retry does not authorize new provider work.
Future API admission must compose reservation and turn acceptance in one transaction;
terminal state and settlement should similarly commit together. No live API/provider,
expiry worker, usage increments or reconciliation service is wired yet. Production rates,
allowances and limits remain Q-005 decisions. 34 database groups passed including ten
concurrent requests, duplicate settlement, admission rollback and crash recovery.

## ADR-039 — Durable terminal turns and database-local outbox consumer (accepted 2026-10-04)

Migration 002 extends the existing schema rather than rewriting migration 001. The
owner-scoped finish_text function atomically commits completed/cancelled/failed state,
version increment, canonical assistant message for completed text only, user-message
status and terminal outbox event. Expected-version checks reject stale writes. Exact
terminal retries are idempotent; conflicting text/state cannot overwrite the winner.
Cancellation and failure release the active-turn slot without fabricating assistant text.
This is text persistence; it does not certify speech was heard or replace voice receipts.

Use one concrete database-local consumer, turn-status-v1, to prove transactional
outbox handling. It locks one pending owner-scoped event with SKIP LOCKED and commits
consumer_dedupe, a version-monotonic status projection and acknowledgement together.
Duplicate delivery changes no projection effects; older events cannot regress state.
The published_at marker currently means this local consumer committed, not delivery
to a broker/provider. No external exactly-once guarantee or background daemon is claimed.
Future external consumers require their own delivery/receipt design and bounded worker
identity. Current runtime context remains server-assigned and transaction-local.

23 real PostgreSQL groups passed, including terminal races, consumer rollback, concurrent
delivery, Unicode persistence and crash recovery of replies and dedupe. Authentication,
quota, policy/provider execution, SSE event history and deletion/retention remain pending.

## ADR-038 — Durable account conversation foundation and guest boundary (accepted 2026-10-04)

Advance independent M2 DATA-01/DATA-03/ARCH-02 foundations while M1 device/provider
gates remain open. Use the specified PostgreSQL stack; this machine's installed 18.1
binary runs isolated synthetic integration tests without Docker/cloud provisioning.
The transactional migration adds owner-scoped users/companions/conversations, accepted
turns/messages, retry keys and an outbox containing references rather than chat payloads.
Composite ownership FKs and forced RLS enforce owner boundaries. Runtime does not own
tables or bypass RLS; actor context is transaction-local. The application must derive
it from verified identity, never client-supplied owner IDs. The test uses a NOLOGIN
runtime role and SET ROLE; this is not a completed authentication integration.

Serialize admission on the user's row, then the conversation, to prevent client-message
dedupe races across conversations. A partial unique index permits one active turn per
conversation. Same-key or same-client-message retries return the persisted turn; changed
payloads conflict. Turn, message, sequence/version, retry key and outbox commit together.
This deliberately simple per-user serialization can be refined after measured contention.
The API does not call this schema yet; quota enforcement, response completion/cancellation,
worker delivery/dedupe, privacy lifecycle, migrations beyond bootstrap and production
identity remain subsequent slices. The schema is not safe to expose as public admission.

Owner answered Q-011: guest trial; account for saved history and purchases. The existing
session-only prototype remains usable without an account. Durable synthetic test rows
represent account users only. No guest persistence/transfer, quota, expiration or consent
policy is inferred. No production data, credentials, external service, release or spend
is authorized by this development work. Project handoff context lives in docs/MEMORY.md.

## ADR-037 — Local speech timing diagnostics (accepted 2026-10-04)

Measure send-to-text, first AudioSource playback command, stream completion, reply
completion and maximum main-thread-observed wait between clips using Unity's monotonic
realtime clock. Replay preserves original generation measurements; each submitted turn
and New chat resets them. A cancelled turn never gains a completed timestamp.
Expose the diagnostic through an explicit Editor menu using synthetic prompts and
numeric-only CSV evidence. Do not record a microphone, save generated text/audio, send
telemetry, change portrait UI or change the user's Editor layout.

This advances PERF-01 instrumentation only: polling is frame-resolution, first playback
is not measured acoustic onset, clip-internal silence is excluded from inter-clip gaps,
and the small local sample is neither cold-start evidence nor a production percentile.
Physical AV capture, Hindi, device resource/latency budgets and provider gates remain open.

## ADR-036 — Constrain local model reply fields (accepted 2026-10-04)

After recurring unusable model output during conversation-context checks, replace
Ollama's generic JSON mode with an object schema requiring text and an enumerated
emotion, with no additional fields. Preserve existing non-empty/600-character text
validation, token/time limits and explicit failure handling. This reduces structural
variation without silently repairing responses or adding automatic model retries.
It does not guarantee every generated response is valid or select a shipping provider.
The real Editor conversation-context regression passed all seven checks after this change.

Codex's machine-local Unity MCP connections are now separately named and pinned by
project path; the Companion connection was verified against Application.dataPath.
This avoids ambiguous routing between concurrent Editors without changing their layouts.

## ADR-035 — Replay last completed reply locally (accepted 2026-10-03)

Cache the current reply's validated speech frames in memory, within the existing three-
frame and bounded-stream limits. Enable Replay only after full successful playback.
Reuse the same PCM, visemes and emotion without an AI/TTS request, transcript insertion
or context commit. Stop during replay must not mark the already-completed exchange as
interrupted or remove it from context. Keep the cache available after stopping replay;
release it on New chat, the next submitted prompt or scene disable. No audio files or
production retention policy are introduced. This supports listening and lip-motion
comparison in the local Editor prototype.

## ADR-034 — Actionable local failure codes (accepted 2026-10-03)

Frame all /turn-stream errors as newline-terminated error events, including errors before
streaming starts. Preserve safe reason codes after text/audio begins; map local model
connection failures explicitly. Unity consumes error frames even for failed HTTP status,
then stops current/queued playback and keeps incomplete exchanges out of context.

Map only known codes to user-facing recovery instructions for busy service, expired
connection, timeout, AI unavailable, speech unavailable and unusable model output. Unknown
codes use a generic message; never display raw response bodies or arbitrary error text.
Transcription guidance asks for another recording/typed input, not Retry of the last chat
prompt. No automatic retry or duplicate generation is introduced.

## ADR-033 — Commit completed local exchanges together (accepted 2026-10-03)

The local scene previously added user context when reply text arrived, before speech
completed. Cancelled/failed turns could leave orphaned user messages, and Retry could
repeat them. Commit the user prompt and assistant reply together after all streamed
clips finish successfully. Retain the existing eight-message bound as four whole
exchanges, dropping the oldest complete pair. Stop/error adds neither side; Retry submits
its prompt once against completed prior context. Visible interrupted text stays marked.
New chat clears context as before. This is session-only development behavior, not a
production memory/retention policy; persistent conversation design remains a later gate.

## ADR-032 — Stable local microphone preference (accepted 2026-10-03)

Remember only explicit device choices in this application's local PlayerPrefs. Device
names are settings, never sent to the voice service. Refresh enumerates devices without
opening them, changing the draft or replacing a previously selected input. A missing
selection stays unavailable until it reconnects or the user explicitly chooses another.
Initial setup with no saved choice keeps the prior first-device behavior; after an empty
startup, newly appearing devices require explicit selection. Prevent Refresh/selection
during recording or transcription. This stores no audio or transcript and makes no
production retention-policy decision. Identically named hardware cannot be distinguished
through Unity's device-name API; physical hotplug acceptance remains separate.

## ADR-031 — Read-only local readiness separate from chat (accepted 2026-10-03)

Keep /health as process liveness. Add authenticated /readiness probing the local Ollama
model list with a 2.5-second timeout; distinguish engine unavailable, missing selected
model and ready. Normalize the implicit latest tag and reject malformed responses.
Coalesce simultaneous probes without occupying the conversation turn slot.

Show a startup check and clickable recheck in the portrait shell using a separate Unity
request and status. A check cannot consume draft text, submit a turn, record audio or
overwrite conversation status. Report recognition as configured only; no inference,
model loading/download, microphone or speech playback test is performed. Missing local
tools remain explicit; this does not change provider policy or automatically install them.

## ADR-030 — Local Whisper trial for English dictation (accepted 2026-10-03)

Owner reports incorrect microphone transcription and confirms English. Add a reversible
local faster-whisper 1.2.1 adapter using the small model on CPU/int8, avoiding driver changes
and external inference. Python 3.11 environment and public model download live under ignored
artifacts; lock dependencies and pin model revision in setup-local-whisper.py. Runtime
loads only local files with Hub offline mode. Audio passes through stdin/in-memory arrays;
no microphone recordings or transcripts are written. Existing 20-second input, timeout,
loopback auth, cancellation and review-before-send remain.

Remove DC, apply at most 8x gain to quiet input, detect silence with local VAD, and decode
utterances separately across long pauses to preserve repetitions. Quiet/clipped/uncertain
warnings are hints, not calibrated accuracy scores. Do not rewrite recognized words with
the chat model. English is explicit; Hindi scope remains unimplemented. Use Whisper when
local installation exists at service startup, Windows otherwise; review state names the
actual recognizer. Whisper failure surfaces an error rather than silently changing engines.

The small synthetic corpus tied with Windows; no improved user-accent accuracy is claimed
without owner testing. This is local evaluation under reversible dependency authority,
not approval of a production provider, paid service, cloud audio transfer or retention policy.

## ADR-029 — Explicit fresh local conversation (accepted 2026-10-03)

Add New chat in the existing portrait header. It cancels active generation, recording,
transcription and queued/current speech, clears the draft, retry prompt, visible transcript
and in-memory model context, and releases the last audio clip/cues. The next request sends
empty history. Keep the selected microphone and the local service available.

This is an explicit development-session reset, not a production retention policy or an
account deletion implementation. It does not promise forensic erasure of process memory
or unload the local model. No persistent chat store exists in this scene. Guard deferred
scroll callbacks against removed labels so immediate resets remain safe.

## ADR-028 — Incremental sentence speech in the local Editor prototype (accepted 2026-10-02)

Keep the installed local model and Windows voice. Deliver full reply text first, then
up to three sequential sentence audio frames over authenticated loopback NDJSON. Unity
plays the first available clip while later clips are synthesized; each clip uses its
own audio sample clock and visemes. Bound bytes, pending frames and sequence numbers;
require an explicit completion frame. Stop/error clears queued clips and prevents late
playback. Add assistant context only after successful playback of the complete reply.
The existing whole-reply endpoint remains available for compatibility.

This reduces speech-preparation wait without selecting a shipping provider. Model text
generation is still buffered, and single-sentence replies have no chunking advantage.
Separate synthesis may change sentence-boundary prosody; listening quality remains a
manual acceptance item. No layout, portrait settings, packages or asset GUID changes.

## ADR-027 — Explicit local recording with reviewed transcription (accepted 2026-10-02)

Continue the Editor-first conversation by adding optional microphone input through the
installed offline English Windows recognizer. No vendor/account/model download. Capture
only after Record is clicked; device selection, elapsed time, input level and a 20-second
submission cap are visible. Stop/focus loss/pause/disable cancel capture. Finish stops
capture and converts PCM to 16 kHz mono before the authenticated loopback transcription
endpoint. Raw capture and recognition operate in memory; no audio recording files or
transcript logging. The 21-second nonlooping capture buffer provides scheduling headroom;
only the first 20 seconds can be submitted. This is a development bound, not product quota.

Review recognized text before a separate Send; do not auto-send uncertain dictation to
the AI. Generated speech tests exposed imperfect Windows recognition, so retain editable
text fallback and clear no-speech/unavailable states. Starting capture stops character
playback to avoid deliberate self-echo. This is turn-based recording, not continuous
duplex voice or production STT. Generated fixtures verify transport/recognition/UI;
physical microphone permissions, noise, disconnection and recognition quality require
manual validation. No Android build or shipping-provider decision in this slice.
## ADR-026 — Include owner-authorized assets with Git LFS (accepted 2026-10-02)

Owner confirms rights and explicitly authorizes all supplied assets in the public repo
for continuation on another machine. Supersede ADR-025's asset exclusions; include original
models, imported FBX/textures, expression exports and character image evidence. Track
FBX and image binaries with Git LFS; retain ordinary Git text/Unity GUID metadata. Do not
rewrite published history. Preserve local bytes and document Git LFS checkout plus Unity,
Node, Ollama/model and Windows voice prerequisites. Generated caches/build outputs and
local secrets remain excluded; they are not portable project source assets.

## ADR-025 — Public source repository excludes local-only CC content (accepted 2026-10-02)

Owner requested commit/push to pushkar-env/ai_companion. GitHub reports an empty public
repository. Publish code, configuration, Unity scenes/metadata and textual evidence;
exclude CC model/texture binaries, character images and raw expression exports while
Q-010 redistribution remains unresolved. Preserve every local file and original GUID.
Document restoring authorized assets for character scenes; the M0/synthetic scenes do
not require those assets. Keep existing master branch, add the requested origin and push
without force. No Git LFS is needed for the resulting small source-only snapshot.

## ADR-024 — Continuous mouth poses instead of phoneme pulses (accepted 2026-10-02)

Owner reported fast, unrealistic lip motion. The first implementation returned each
phoneme to neutral using a 25 ms envelope, creating repeated opening/closing even across
equivalent vowels. Replace that envelope with audio-clock coarticulation: smoothstep
crossfades around adjacent cue boundaries (up to 55 ms each side, shortened for short
cues), followed by frame-rate-independent 45/75 ms shape easing and 85 ms jaw easing.
Accumulate equivalent mappings so repeated vowel cues hold a continuous pose. Reduce
most lip strengths from 65% to 42% and maximum provisional jaw opening from 7.2 to
4.48 degrees. Silence/natural completion ease closed; explicit interruption resets
immediately. Speech speed and original cue timestamps remain unchanged. Pure motion
logic lives in Companion.Core; existing rigs, scene GUIDs and Editor layout are preserved.
Automated continuity/playback checks do not establish final perceptual lip-sync quality.

## ADR-023 — Playable local talking character before more builds (accepted 2026-10-02)

Follow the owner's explicit Editor-first priority. Add TalkingCompanion as a separate
scene copied from the existing CC test scene, retaining all source GUIDs and original
scenes. Runtime-only relaxed arms, idle head motion, blinking, SAPI mouth cues and four
bounded expressions animate the existing rig; no source rig or material edits.

Use the already-installed qwen2.5:7b through Ollama on loopback, plus Windows Microsoft
Zira Desktop synthesis. This is a reversible local evaluation adapter, not a shipping
provider choice, clinical/content safety system or Hindi voice implementation. No new
packages/models, accounts, credentials or spending. A Node service outside Unity Assets
owns generation/speech and exposes an ephemeral authenticated localhost endpoint; its
random token/config stays under ignored artifacts and never enters scene serialization.
It has a single active turn, bounded input/output and cancellation/deadline handling.
The UI receives a complete generated reply and PCM before playback; it is not streaming
audio. Keep eight in-memory context messages, omit interrupted assistant replies, and
clear context on Play exit. There is no persistent memory or microphone input.

Use native 16 kHz mono PCM for this installed SAPI voice. Testing 24 kHz output exposed
cue-clock mismatch, so do not resample these engine events independently. Map cues from
AudioSource.timeSamples to provisional CC visemes and jaw assist. Expressions use a
validated neutral/happy/concerned/curious enum. This is working animation, not calibrated
phonetic/perceptual lip-sync acceptance. Block building this Editor-only scene.

Preserve portrait configuration, Editor docking/sizing and Simulator selection. Start
the local service through the Companion menu/Play entry; no layout helpers or Android
builds. Remaining real-time/mobile/provider requirements are still partial.
## ADR-022 — Synthetic loopback playback HTTP boundary (accepted 2026-09-29)

Exercise receipt authorization and validation over actual HTTP before Unity networking.
Use Node's existing built-in server, bind only IPv4 loopback on an ephemeral port and
register delivery fixtures through the trusted in-process API. Random temporary bearer
tokens map to synthetic accounts; clients cannot register or override delivery metadata.
Reject foreign Host/browser Origin, unexpected fields, oversized bodies and conflicting
retries. Five-minute token lifetime, 200 requests per token and 100 delivery records are
test harness bounds, not product decisions. No dependencies, accounts or external calls.
The automated runner controls startup/shutdown and keeps credentials only in memory.
Native HTTP requests exercise exact wire headers; fetch rewrites the Host test header.
Production identity, TLS, durable receipts and independent delivery evidence remain open.
This separate voice harness does not bypass the blocked .NET API executable. Unity,
portrait settings, scenes, source models and Editor layout are unchanged.

## ADR-021 — Match playback receipts to registered delivery (accepted 2026-09-29)

Wrap the offline sample report bridge in a bounded local receipt store. The trusted
caller registers account/session/utterance/epoch, fixed audio format, delivered sample
count and alignment. Reports cannot increase delivery or change timing format. Preserve
the first finalized receipt: identical retries return it; changed retries conflict.
Newer registrations invalidate unfinished old reports while finalized receipts remain
replayable. Clone registration inputs, release draft alignment after finalization and
fail admission at capacity rather than evicting replay records. Capacity is a fixture
memory bound, not retention policy. No public endpoint, database, real authentication,
provider or Unity change; production must establish trustworthy delivery independently.

## ADR-020 — Observe Unity playback before resetting audio (accepted 2026-09-28)

CC lab records the synthetic utterance ID, playback epoch, sample rate, clip sample
count and highest observed AudioSource.timeSamples. Capture before Stop resets the
cursor; retain the first terminal report across duplicate resets. A stopped source
does not certify full playout, so preserve observed progress without inferring missing
samples. Display the result inside existing portrait scroll controls.
The backend bridge validates identity/sample bounds and rounds observed milliseconds
down before conservative context rebuilding. It is an offline boundary, not a trusted
client endpoint or proof of hearing. No real transcript is attached to the tone.
Actual Editor-exported data is tested with explicitly synthetic text timing. Production
still needs authenticated delivery, native output latency validation and real alignment.

## ADR-019 — Conservative heard-response context (accepted 2026-09-28)

Prepare VOICE-03 context rebuilding with an in-memory per-response tracker under the
existing voice worker directory. Require explicit aligned text segments and monotonic
playout position for a session/generation epoch. Only segments fully played at the
terminal position enter context; omit a partially played segment rather than estimating
words from duration or string length. Validate ordered timing and grapheme boundaries
so Hindi combining marks and emoji are not split. Missing alignment yields no inferred
text. Preserve interruption status and duration; late reports cannot rewrite finalized
history. Release the unheard draft from the tracker when finalizing.

These are conservative local bookkeeping rules, not an approved retention policy or
proof of what a person actually heard. A real integration must obtain the local audio
playout clock, reconcile trusted alignment and session ownership, stop immediately, and
persist canonical history transactionally. No provider, region, pricing or API chosen.

## ADR-018 — Acknowledged local voice action delivery (accepted 2026-09-27)

Extend the simulator with stable action IDs, non-consuming pending reads and explicit
acknowledgements. The local worker acknowledges only success, limits retries and action
duration, rejects overlapping pumps, and keeps exhausted work visible for explicit retry.
Keep flush/stop independent of cancellation delivery, and hold simulated settlement until
stop acknowledgement. Fake sink effects deduplicate by action ID and reject stale epochs.
Inject failures before effect and after effect/before acknowledgement to test both cases.

This is intentionally in-memory and provider-neutral. Worker recreation tests retain the
same coordinator/sink objects; they do not prove persistence across process loss. A remote
adapter must provide its own abort/idempotency/reconciliation semantics. No vendor call,
account, financial policy, Unity change or package installation is implied.

## ADR-017 — Local backend voice-session lifecycle (accepted 2026-09-27)

Advance VOICE-02/03/04 lifecycle preparation without selecting a provider. Add a pure
TypeScript in-memory coordinator under services/voice-agent, using the already pinned
Node/TypeScript toolchain and no dependencies. It takes explicit timing limits and an
injected monotonic clock so lease loss, late frames, interruption and duplicate commands
can be tested deterministically without timers or external calls.

One active session is scoped to an account/companion pair. Keep terminal connect records
for replay; cap total local records rather than evicting idempotency history. Generation
epochs invalidate late media. Stop/settlement actions are emitted once per local terminal
transition, but they are neither durable outbox deliveries nor provider billing events.
The worker scheduler, authentication, media, transport and real metering are future work.
Fixture lease/duration values do not set product pricing or retention policy. No Unity
asset, Editor setting, package version or provider approval is changed.

## ADR-016 — Record isolated build input provenance (accepted 2026-09-27)

The repository has no committed revision yet, so an APK timestamp alone cannot identify
its source. Before launching Unity, compare SHA256 hashes and paths for all Assets,
Packages and ProjectSettings files in the original project and copied build project.
Abort on missing, extra or changed inputs; save both inventories alongside the APK.
Keep Library/UserSettings out of the snapshot and do not overwrite prior evidence.
`tools/verify-build-snapshot.py` also rejects evidence paths inside input folders.
This is copy-integrity evidence, not a deterministic-build or runtime-quality claim.
The first use was checked during import before the build entry point; future runner
invocations perform this check before launching Unity. Actual 272-file comparison and
matching/mismatch/output-protection fixtures passed.

## ADR-015 — Isolate jaw motion before bone conversion (accepted 2026-09-27)

The exported V_Open and Jaw_Open expression records differ, including extra bones for
Jaw_Open. Archive their exact source records/hash; do not infer Unity units, handedness,
rest-space or multiplication order from quaternion components alone. Keep the existing
prototype axis/default and add session-only 0–30 degree adjustment and a bone-only
preview. Reset/cancel/background clear skeletal poses; changing calibration cancels audio.
This reversible diagnostic supports comparison of morph-only, bone-only and combined
poses without writing to the scene or original rig. It does not claim calibrated lip sync.
120 Editor checks passed, including actual skinned-geometry movement/reset and invalid
input rejection. Portrait and the existing Simulator/window layout are preserved.
Source records and evidence: evidence/m1/jaw-comparison/. Android rebuild unexecuted.

## ADR-014 — Implement exported partial-blink correctives (accepted 2026-09-27)

Source JSON explicitly supplies two single-input linear Add curves: Eye_Blink_L/R drive
C_BlinkL/R through (0,0), (.5,1), (1,0). Apply these curves to native lab blink weights,
add a half-blink inspection button, and retain both corrective names in mobile build
copies. Source rule/hash evidence is in evidence/m1/blink-correction/. No full CC solver
or inferred bone conversion: other compound/Limit rules and jaw metadata remain separate.
108 portrait Play Mode checks and 135-frame subset equality checks passed. The existing
169.4 MB APK predates this change and must not be represented as containing it.

## ADR-013 — Retain only exercised CC morphs in diagnostic mobile builds (accepted 2026-09-26)

Measured build data attributes 88.7% of uncompressed user asset bytes to meshes. Use
IProcessSceneWithReport to clone the CC lab's meshes only in development Android/iOS
builds and retain its 30 exercised native names. Preserve full source/imported assets,
scene GUID, geometry, materials, skinning and every retained delta/frame. Reject an
authored nonzero omitted shape. The existing non-development scene guard still applies.

Exact-frame and base-geometry comparisons passed for 129 retained frames across eight
renderers; 1,379 unused shapes are omitted from the build copies. Keep this scoped to the
diagnostic scene. A production avatar needs its own approved versioned profile and LODs;
do not infer that a limited diagnostic subset fulfills the canonical-52 requirement.
No texture downsampling, package removal, Unity upgrade or layout change is justified by
this experiment. Verify actual APK size after building; do not estimate it from source size.
Result recorded 2026-09-27: the Android build passed, with a 169,373,218-byte APK versus
563,534,978 bytes previously (69.94% saved). Portrait/debug/ID/ARM64 checks passed.
The mesh category fell from Unity's rounded 842.9 mb to 5.1 mb; textures stayed 75.8 mb.
This verifies diagnostic package reduction, not production profile/device performance.

## ADR-012 — Measure native morphs before changing calibration (accepted 2026-09-26)

Use an Editor-only deformation audit on a disposable CC import instance. Bake vertices
at 25/50/100 percent, report per-mesh motion and reset drift. The previous runtime weight
assertions establish bindings but do not establish visible geometry motion. Keep audit
reports outside Assets. Do not infer perceptual gains or bone transforms from displacement
alone, and do not modify the source/export or open scene. Ninety probes passed; per-mesh
coverage exposes named zero-motion shapes for later fidelity review. This adds no player
dependency, package or layout changes.

The owner also authorized another isolated Android build after freeing space. This
does not revoke Editor-only interaction testing or authorize device installation/release.
Voice cost/processing research remains a proposal; no vendor selection is accepted here.
The retry built successfully. Packaged portrait/debug/ID/ARM64 checks passed. Corrected
aapt2 decimal/boolean parsing after inspecting the actual manifest; wait for Unity's
process only to avoid hanging on reusable Gradle descendants. No full rebuild solely for
the wait fix. APK size is above target; do not treat successful compilation as mobile
performance acceptance or justification for unreviewed package removal.

## ADR-011 — Portrait enforcement and Editor layout preservation (accepted 2026-09-26)

Owner explicitly requires portrait mode throughout development/builds and preservation
of the Unity Editor layout. Retain the portrait-only PlayerSettings, add a mobile build
preprocessor that rejects incompatible orientation flags, and set portrait before scene
load in Android/iOS players only. Never change Game-view size from runtime code.

Remove automatic PortraitPreview calls from scene-opening menus. The explicit preview
menu remains available for the owner to invoke; automation does not invoke it. Do not
move/dock/resize windows or load layouts. Record current window geometry for comparison.

Android diagnostic builds use a fresh ignored source snapshot (Assets, Packages and
ProjectSettings only) in a hidden batch Editor. Main apps/unity stays in place, with
its target, layout, scene list and signing configuration unchanged. Development APK
uses local debug signing and a test-only application ID; no custom key or publication.
The build targets ARM64 IL2CPP and includes only the CC character lab. A packaged-manifest
check is provided for portrait, debuggable flag and ARM64 IL2CPP library; it is unexecuted
because the first build failed at linking with a no-space-left error. A 20 GiB free-space
preflight now covers workspace/temp volumes without automatic cleanup; it cannot guarantee
peak linker space. Owner subsequently restricted current testing to Device Simulator.
Use the existing pane without rearrangement; both portrait profiles passed 98 checks.
This is not evidence
of physical device orientation, rendering, thermal behavior or production readiness.

## ADR-010 — Native CC character test scene (accepted 2026-09-26)

Owner authorized using the supplied character for testing and building its test scene.
Create CCCharacterTest separately, keeping MockCompanion and FacialDiagnostics intact.
Use the existing imported copy, persistent basic URP Lit materials and extracted normal
maps; no vendor plugin or package change. Original export remains untouched.

Expose 29 native CC controls and coordinated same-name weights across face/brows/other
meshes. Provide blink/smile/frown/open presets and deterministic tone-driven mouth cues.
Use a labeled, switchable provisional local jaw rotation for the open test; this does
not implement the complete JSON bone-expression profile. Do not present the controls
as the production calibrated canonical-52 adapter. Frame timing uses AudioSource samples;
stop/background/audio-configuration callbacks clear audio and restore rest. Scrollable
controls preserve the portrait preview and fixed reset action. Block non-development
builds that process the test scene. No production character selection or rights implied.

## ADR-009 — Inspect owner CC export in isolation (accepted 2026-09-26)

User supplied the root models export and requested suitability assessment. Preserve all
121 source files and hash them. Import an FBX copy under Companion/Imported/CC5Inspection,
extract embedded textures and configure that copy as Humanoid. Use a disposable preview
scene for baked facial probes/captures. Do not replace the application avatar or add
vendor packages for an assessment. The asset is suitable for prototype integration with
CC-name/multi-mesh/bone adapters; it is above mobile geometry/material targets. Keep
optimization as a derivative and licensing as an explicit Q-010 gate. See the CC5
assessment for measured evidence, limitations and source hashes.

## ADR-001 — Preserve the installed Unity project (accepted 2026-09-26)

Keep apps/unity in place, Unity 6000.5.9f1 (b57deb96f08d), URP 17.5.0,
Input System 1.20.0 and the existing packages-lock.json. No package upgrades or
render-pipeline migration. Existing scene, settings, assets and GUIDs are preserved;
docs/evidence/pre-m0-files.json records 36 original asset/package hashes.
The live Editor uses PC_RPAsset; Mobile_RPAsset also exists. Existing AI assistant
2.20.0-pre.1 and pipeline 0.7.0-exp.1 are compatibility risks to assess before mobile
release, not grounds for an untested change. M1 must prove Android/iOS IL2CPP/native SDKs.

## ADR-002 — UI Toolkit and a pure C# mock domain (accepted 2026-09-26)

UI Toolkit ships with the existing Editor and supports the current Input System without
introducing a legacy input path. Separate Core, Presentation and Editor assemblies.
The Core has no UnityEngine dependency and runs in .NET tests. Composition is local to
the demo scene; no global singleton, microphone, external provider or persistence.
The runtime UI uses a RenderTexture portrait of original primitive geometry, selectable
text, labeled states, reduced motion and larger text. This is an Editor prototype;
Physical safe area, keyboard, screen readers, Hindi typography and 200% text are M1/M5
evidence gates. ADR-007 supersedes the initial landscape layout with portrait UI and
Editor-tested safe-area/keyboard simulations.

## ADR-003 — Lightweight M0 local environment (accepted 2026-09-26)

Use the already-installed .NET SDK 10.0.301 pinned in global.json and ASP.NET Core net10.0
for an optional loopback-only contract simulator. [.NET 10 is LTS](https://dotnet.microsoft.com/en-us/platform/support/policy).
Unity runs without it. Python 3.14.2 with pinned jsonschema dependencies validates schemas;
Node 24.12.0 and TypeScript 5.9.3 (package-lock.json) check the generated TypeScript
consumer; no React/admin runtime is installed for the reserved admin boundary.
Default M0 bootstrapping deliberately omits idle Postgres/pgvector, Redis and object-store
containers. This is a reversible REPO-02 deviation for the requested lightweight demo;
add and test the version-pinned Compose profile with M2 durability work. No production
runtime or database compatibility is claimed. .NET SDK patch updates require normal review.

## ADR-004 — Product decisions and approval boundary (accepted 2026-09-26)

User approved adults-only, non-explicit companionship with no clinical claims; India;
English and Hindi; original stylized adult avatar with a temporary neutral brand.
See QUESTIONS.md for exact answers. English-only M0 is a labeled technical fixture,
not a reduction of the bilingual launch scope. No legal, retention, provider, pricing,
data region, accounts or spend approval follows from these answers.

## ADR-005 — Deterministic local behavior and contract boundary (accepted 2026-09-26)

Use versioned JSON Schema 2020-12 and OpenAPI 3.1; derive C#/TypeScript DTOs with a
repository generator and check drift. API and Unity compile identical DTO output.
Fixed responses, event IDs/timestamps and injected scenarios keep checks reproducible.
Retry is explicit, at most twice, and replaces the visible attempt without duplicating
the user bubble; replay intentionally creates a new local turn. No automatic paid retry.
The optional HTTP service supports action/message dedupe, cancellation and in-memory SSE
replay. It is not a durable/authenticated service. API and Unity are independent mock
hosts of the same domain; the scene does not currently call HTTP.
Session history is volatile (20-turn UI bound; 100-turn API fixture bound), not an
approved retention policy. Reset affects local memory only. Mock startup and scene-build
guards reject production startup/non-development builds containing the mock scene.
These are local safeguards, not a production security boundary.

## ADR-008 — Synthetic M1 diagnostic boundary (accepted 2026-09-26)

Owner has no production assets and explicitly authorized synthetic diagnostics. Add a
separate portrait FacialDiagnostics scene: 52 named blendshape pads and 15 deterministic
viseme cues with a generated tone, visibly labeled as synthetic. Preserve the M0 scene.
The tone is not speech and the board is not a humanoid avatar. No new vendor/native SDK,
microphone, provider account or package change. Guard this scene against non-development builds.

Keep normalized facial data, bounded frame queues, utterance/epoch cancellation and mock
transport in the pure C# core. Unity applies 0–100 renderer weights from the AudioSource
sample clock. Background and audio-configuration callbacks flush playback. This proves
local mechanics, not acoustic output latency, actual route handling or perceptual lip sync.
Current one-channel diagnostic mappings must be replaced with calibrated avatar mappings.

First available Android target is owner-reported Galaxy S23, Android 16 / One UI 8.
It is not an approved minimum support tier. Owner will install Android tools; USB access
is unconfirmed. No iPhone is available; Mac/Xcode access is unknown. M1 stays partial
until physical Android and iOS exit evidence and approved provider/asset work are complete.

## ADR-006 — Repository initialization and preservation (accepted 2026-09-26)

No Git repository existed at root or apps/unity. Initialized Git at root, preserving all
files, and added ignore/attribute rules. No remote, commit, push or release was created.
The original sample scene/build list stays unchanged; a separate MockCompanion scene
and menu entry provide the startup path. Keep all generated Unity .meta files in source.

## ADR-007 — Portrait-first mobile shell (accepted 2026-09-26)

User explicitly corrected the app to portrait mode and requested implementation through
M0 again. Use portrait startup with landscape/upside-down autorotation disabled, and
390×844 width-based panel scaling. These PlayerSettings changes are authorized by the
request; no Editor, pipeline, package, scene or asset GUID changes are needed.

The mobile UI uses a single vertical column: persistent mock badge, visible character,
scrolling conversation and bounded composer/recovery actions. Demo controls use an overlay.
Larger chat text applies to subsequent turns as well as existing labels. Apply safe-area
insets and compact (do not hide) the character when the keyboard reports occlusion.
TouchScreenKeyboard area reporting/OS window resize must still be tested on devices;
Editor simulated insets are not evidence of native IME behavior. Large desktop windows
retain a centered portrait shell; there is no landscape two-column product mode.

M0 checks target 360×640, 390×844, 1080×1920, simulated notch/home indicator and keyboard,
long messages and larger text. M1 extends these to physical portrait rotation, gestures,
actual keyboards and English/Hindi. Earlier product/provider/retention gates remain intact.

## ADR-042 - Supplied character roster in the local talking scene (2026-10-04)

Accepted reversible implementation of the owner's Alita/Cosmos integration request.
Preserve source files and existing asset GUIDs, import into separate folders and use
separate URP materials/extracted textures. Register three scene references on the existing
controller; Original remains the default. One active rig and three resident rigs give
immediate Editor swapping; production streaming/loading policy is not selected.

Appearance switching restores the old pose and rebinds bones, facial channels and portrait
framing without resetting the audio clock, chat, draft, microphone, request or Replay cache.
Map extended CC smile/frown/inner-brow/wide-eye names to existing semantic controls.
All three rigs have the ten speech channels across body/tongue meshes. Selection is
session-only; no separate companion personality, identity, retention or paid catalog
policy is inferred. Q-016 remains open for production scope.

Use URP Lit with packed alpha as a prototype conversion. Cosmos's procedural RLEyeOcclusion
has only a white mask and no baked shadow texture; make that overlay transparent pending
a dedicated shader/art pass. Preserve the original FBX and render pipeline.

The previously configured qwen2.5:7b was absent during testing; qwen3:8b was already
installed locally. Use it via ignored machine-local model.txt, below the environment
override in precedence. Disable thinking in local requests so the bounded response budget
is available for structured spoken text. Repository fallback and shipping-provider choice
remain unchanged. No download, cloud service or paid account was used.

Evidence: 40 character checks, 11 generated-audio transcription checks with a character
change during recognition, 12 endpoint boundary checks and 8 readiness checks passed.
Mobile and production asset quality are not established by these Editor tests.

## ADR-043 - Alita-only prototype and measured asset optimization (2026-10-05)

Owner explicitly requests keeping only Alita, removing the other two models, polishing
and performance testing. Supersedes ADR-042's active three-character roster. Remove
Original/Cosmos scene instances and imported assets from Unity's Assets tree; keep original
source exports and archive imported data, .meta GUIDs, prior scenes and obsolete roster
tools under models/archived-unity for reversible recovery. This does not authorize deleting
source artwork or decide final production customization beyond the current Alita-only scope.

Keep the reusable character adapter but display a static Alita title when only one option
exists. Both talking and facial-diagnostic scenes use Alita and retain scene GUIDs.
Generate separate runtime meshes with exact base geometry, UVs, skinning and retained
morph frame deltas; omit unused morphs only. Facial updates gather per-frame targets and
write only changed weights, while Stop/reset still flushes immediately. Keep all ten
speech channels plus blink/emotion mappings. Source FBX remains untouched.

Use 2048 hero diffuse textures, 1024 hero normals and 512 supporting textures, compressed
with mipmaps. Improve hair coverage, normal strength, eye highlights, portrait framing,
warm/cool lighting and 4x render-target antialiasing without changing pipeline/packages
or Editor layout. Production hair/skin shading and mobile LOD authoring remain separate.

Measure the same Alita view in uncapped desktop Editor/Simulator: 30 s idle and 30 s real
cached speech, five-second warm-up, before/after; record raw frames and a scoped character
Update profiler marker. Asset sizes are referenced resources, not OS resident memory.
Do not equate these short desktop tests with mobile thermal, resident-memory, acoustic
sync or release-device acceptance. Preserve PERF-01 targets; document remaining misses.

## ADR-044 - Atomic metered terminal transition (2026-10-05)

Add migration 005 and a SECURITY INVOKER database-worker entry point,
finish_metered_text(turn, expected_version, state, text, actual_units). Lock the active
owner first, then the owned turn, require its bound reservation, and invoke existing
terminal and usage primitives in one transaction. Any settlement failure rolls back
reply text, sequence/version changes and terminal outbox event as well as quota changes.
Exact retries reuse both terminal result and ledger row; changed usage/text conflicts.

Grant this entry only to a NOLOGIN companion_worker role inheriting the trusted runtime
role. The account API receives no new completion endpoint or worker membership. Existing
low-level runtime SQL grants remain internal trusted-server capabilities; this is not a
complete separation of all table privileges or an external worker authentication system.
Actual units must come from trusted usage evidence, never a client body. Cancellation
and failure accept explicit observed units; synthetic zero/nonzero tests do not set a
production charging policy. Usage above the reservation fails closed for reconciliation.

Verified in disposable PostgreSQL 18.1 with synthetic accounts: rollback, idempotent and
conflicting retries, owner isolation, cancelled/failed turns, two concurrent completions,
and immediate-stop/restart recovery. Total 42 database groups and 23 existing real HTTP
checks passed. No real data, cloud resource, public route or Unity retention change.

## ADR-045 - Local synthetic worker leases and fenced completion (2026-10-05)

Migration 006 adds owner-scoped worker_leases and worker-only claim/renew/finish functions.
Claims lock owner then turn, select one funded accepted turn, and issue a fresh random
token. Bounded expiry/renewal uses database time. Reclaim replaces the token; stale or
expired holders cannot finish through the leased entry. Completion composes migration
005 and marks the lease finished in the same transaction. Exact winning receipt retries
remain valid after expiry; changing payload/usage still conflicts. An in-flight lease
does not create a new public turn state or consume the existing status-consumer outbox.

A bounded Python/psql worker demonstrates actual queue-to-reply behavior without new
packages: one pass, one fixed labeled synthetic response, zero provider usage, no polling.
It requires explicit local/synthetic mode, loopback and a non-owner worker login; it emits
no tokens, prompts or credentials. The existing local AI Unity service is not connected
to persistent account history by this change.

Expiry/reclaim is validated only for synthetic work. Before a paid/provider worker, add
provider idempotency/unknown-outcome reconciliation, trusted worker identity and cancellation
semantics; expiry must not imply a provider did no work. Existing trusted runtime SQL
privileges remain, so this is cooperative entry-point fencing, not complete database
capability separation. Production guest/retention/provider policies remain open.

Verification: 51 database/worker groups plus 23 actual HTTP checks passed. Nine new groups
cover competing claims, role/owner isolation, renewal, expiry/token replacement, failed
settlement rollback, completed receipts, rollback of a claim, a real non-owner worker
process and rejection of production/non-loopback/privileged configuration.

## ADR-046 - Durable local replay and cancellation fencing (2026-10-05)

Migration 007 assigns each outbox event a monotonically increasing conversation cursor
under the conversation row lock, in the same transaction as admission/terminal state.
Rollback restores the counter. The existing outbox doubles as a replay log; status-consumer
acknowledgement does not remove it. Existing synthetic events receive deterministic
reconstructed ordering (turn creation/id then aggregate version), not a claim to recover
historical commit order. No real-data retention duration is selected by this migration.

The synthetic account API adds bounded owner-scoped events/messages JSON pages with
transaction-local RLS and repeatable-read snapshots. Cursors are exclusive and scoped to
one conversation and endpoint; limits default to 50, maximum 100. Message pages contain
canonical current status, not a status-change feed; clients use turn events to refresh
older messages. Event pages are durable terminal/admission events, not token deltas or
live SSE. Completed events read their text from the immutable canonical assistant row.

Cancellation accepts only expected_version and delegates to serialized terminal state.
An exact retry returns the same outcome. A completed turn cannot later be cancelled;
a cancelled turn cannot publish a late assistant reply, even if a worker already holds
a lease. Cancellation keeps the reservation until trusted usage reconciliation: client
cancellation is not evidence a provider did no work. A still-valid lease can acknowledge
the cancelled result with observed usage; after expiry a trusted reconciliation path is
required. There is no automatic refund or production billing decision here.

Unity remains session-only; portrait settings, assets and Editor layout are untouched.
Production identity/provider execution, live SSE, retention/deletion policies and unknown
provider-outcome reconciliation remain separate gates. See replay-cancellation evidence.

## ADR-047 - Bounded SSE over the durable synthetic event log (2026-10-05)

The local account API exposes GET conversations/{id}/stream using the same persisted
outbox cursors as JSON replay. Last-Event-ID resumes exclusively; an explicit after
query can initialize a cursor, but conflicting query/header values are rejected.
SSE frames carry the sequence as id, the stored event type, and the canonical event
JSON. No provider call is restarted by reconnecting. Clients must persist their last
processed cursor per conversation and deduplicate events across interrupted processing.

Local transport defaults: four concurrent streams total, 50-event read batches,
500ms polling/heartbeat comments, 30-second connection lifetime, and 1-second reconnect
hint. These reversible development bounds are not production capacity promises.
Each read has a new owner-scoped repeatable-read transaction; pooled database connections
are released before network writes or waits. Active owner status is checked every poll.
Token expiry bounds the active connection, including blocked writes; disconnect releases
the stream slot. After response headers, storage failures abort transport rather than
append a JSON error to an SSE stream. Reconnect recovers from the durable cursor.

This is admission/terminal event streaming, not model token streaming or Unity account
integration. No dependency changes, provider activation or real-user retention change.
Production identity refresh/revocation, fair per-user limits, proxy/load/backpressure
measurements and client reconnect UI remain further work.

## ADR-048 - Session-only synthetic history client foundation (2026-10-05)

packages/account-client contains a .NET 10 transport and event projection, kept outside
Unity Assets. It deliberately does not attach synthetic account persistence to real
prototype conversations. One immutable endpoint/token/conversation scope owns each client;
account switching creates another instance, with no shared cursor or on-disk credentials.

History hydration and SSE follow use the durable event contract. The cursor advances only
after successful projection; replay duplicates are ignored and gaps fail closed. A
truncated frame never advances the cursor. Events update one turn rather than appending
duplicate UI rows; cancellation/failure produces no fabricated assistant reply. A bounded
UTF-8 SSE parser supports comments and split frames. Reads are serialized; callbacks run
after application, so callback exceptions do not roll back the committed client projection.

EOF/transient failures reconnect at most three times with bounded backoff/jitter and a
45-second connection/read deadline. Other HTTP failures require caller action. Cancellation
stops recovery; it is distinct from requesting turn cancellation. The client never sends
an admission as part of reconnect. Local history/frame limits bound retained data.

This is a tested client foundation, not Unity integration: net10.0 APIs require a separate
Unity-compatible transport/serialization adapter and Editor checks before UI wiring.
No added package dependencies, production identity/provider choice, or retention policy.
