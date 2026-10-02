# Technical decisions

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
