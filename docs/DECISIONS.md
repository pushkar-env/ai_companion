# Technical decisions

## ADR-075 — Modular characters and a category-gated wardrobe, starting with Arjun (2026-10-10)

The owner asked to make the characters modular so outfits can be customised and swapped while the
overall look stays the same, and to add the outfit from their reference image to Arjun first: an
open light-blue chambray shirt over a white crew-neck tee, olive chinos, white sneakers and a steel
watch. Female characters follow later. A male character must never be offered female outfits, and
the other way round.

**Decision.**

- **Base body plus garments.** A modular character is a base body (skin, hair, eyes, mouth and the
  whole face rig) plus garments: separate skinned meshes, one FBX per garment
  (`Imported/<Name>/Wardrobe/`), on the same skeleton. The import binds each garment to the
  character's own bones by name and grafts garment-only spring bones under their parents.
- **Garment data.** `CompanionGarment` on each garment holds the WARD-01 fields we need now: id,
  display name, category, fitted body, slot (top, bottom, shoes, accessory), its spring chains and
  the body renderers it hides (occlusion masks).
- **Profile.** `CompanionWardrobeProfile` on the character root holds the body's category, its
  fitted-body id and the preset outfits; the first preset is the signature look.
- **Category rule.** `CompanionWardrobe` wears and offers only garments whose category and fitted
  body match the profile. Other garments are hidden, never listed, and `Equip` refuses them. A body
  without a profile never wears modular garments. Checked both ways.
- **Arjun.** A base body and six garments:
  - his original outfit, split out and visually unchanged (mean pixel difference 0.1 against the
    earlier render): brown shirt, grey trousers, white sneakers;
  - chambray shirt with white tee (one mesh, three materials), olive chinos, steel watch.
  - Presets: Signature look (default) and Chambray casual. Both trousers share one fitted mesh.
    Mix & match works per slot, and the accessory slot may be empty.
- **Physics.** Each shirt owns its spring chains: 8 hem chains for the brown shirt; 2 three-bone
  open-edge chains plus 5 hem chains for the chambray shirt. Chains of garments not worn are paused
  (`CompanionSecondaryMotion.SetChainPaused`).
- **Style panel.** Modular characters get Outfit (presets plus Mix & match), Top, Bottom, Shoes
  (when there is a choice) and Accessory pickers above the existing colour and skin-tone choices.
  The saved look stores `outfitId` and the garment per slot. Alita, Meera and Tara are unchanged.
- **Textures.** New garments use procedural 2048 px atlases (watch 512 px). The signature shirt and
  sneakers keep the original atlas; the grey trousers are its re-bake on the new layout.
- **Later.** Female garments; on-demand loading, remote catalogue, entitlements and server equip
  (WARD-01, ASSET-01); garments shared across bodies (today each garment is fitted to one body).

**Verification.** All pass:

- Arjun: 127 rig checks (20 new wardrobe checks) and 32 in-app checks (outfit round-trip through
  Style).
- Regressions: Meera rig 105 and in-app 27, Tara rig 106 and in-app 27, face performance 68, body
  idle 34, Alita wardrobe UI 8, skin-tone material 13 and UI 24.
- Blender pose tests: no tee poke-through relaxed, arms raised or twisting one way; one point
  crosses by 9 mm in an extreme combined bend and twist.

**Limits.**

- The chambray sleeves stay rolled at the elbow like the signature shirt; the reference shows them
  a little lower.
- Procedural fabric textures; the open shirt is derived from the original closed shirt.
- All garments load with the character; no device or performance evidence.

**Owner review fixes (2026-10-10).** The owner found two faults with the chambray outfit: shirt
colour on Arjun's neck, and the shirt stretching unnaturally at the armpits when he raises his arms.

- **Neck.** The split had left shadowed neck skin inside both shirts. The brown shirt kept the
  original texture, so it still looked like skin; the chambray copy painted it blue.
  - Those 668 faces now belong to the base body, restored from the pre-split mesh with their
    original weights and face-shape data.
  - The tee's neckline was lifted where the restored skin came through it.
  - The chambray collar's saw-tooth back edge, the torn end of the right lapel and loose flaps at
    the collar sides were cleaned up.
- **Armpits: share joints.** Each shirt now brings a half-rotation share bone per shoulder,
  `Share_<side>_Upperarm`. It sits under the clavicle with the upper arm's rest pose.
  `CompanionSecondaryMotion.shares` turns it by half the upper arm's rotation every step, including
  reduced motion. The garment's `item.json` lists the share joints, and the import grafts the bones
  like chain bones.
- **Armpits: weights.** The underarm of both shirts was re-weighted across the fold: chest, then
  share bone, then arm. The side panel no longer carries upper-arm weight 10–15 cm below the armpit.
  That weight had dragged it into a web from elbow to hem. With the arms down the look is unchanged.
- **Signature look.** The brown shirt received the same fixes. Its idle look is unchanged.
- **New checks.** Rays toward the neck axis must hit skin before any garment (26 of 26 per outfit),
  and the share joints must turn exactly half the upper arm. The shirt's side below the armpit must
  stay within 3 cm of chest-rigid motion as the arms rise. The new measured values are 1.1–1.2 cm in
  the yawn and 2.1 cm in the side stretch; the old weights measured about 5 cm in Blender.
- **New renders** per outfit: neck close-up, plus armpit at arm level, raised and side stretch.
- **In-app check.** It now starts from the default look whatever the device saved, and restores
  the saved look afterwards.

## ADR-074 — Arjun: first male companion, with voice and name per companion (2026-10-09)

The owner supplied a third Tripo GLB, a stylised young man (`3d boy model.glb`), and asked for the
Meera/Tara treatment: cloth physics, blendshapes, lip-sync and facial animation, "alive",
polished and realistic in the app. The owner gave no name; "Arjun" is a placeholder.

- **Adult presentation:** he has adult body proportions (about 7 heads tall) and is scaled to
  1.78 m including hair.
- **Roster:** Alita stays the scene default; the roster is now Alita, Meera, Tara, Arjun.
- **Unchanged:** Q-016 (production identity and roster) and Q-010 (Tripo rights) stay open.

**Rig.** It follows the project skill, including the stage-12 face polish:

- 84k-triangle body; 92 deform bones (58 CC_Base body bones and 34 spring bones in 10 short-hair
  chains and 8 shirt-hem panels); Blender-only IK and look controls.
- 76 body channels, rotatable eyeballs, eyelid shells and a cut mouth with interior.

New techniques for this model:

- **Shoes and trousers:** split by a watershed on colour edges; the trouser hem covers the white
  sneakers.
- **Fringe and painted brows:** told apart by a ray test into the head.
- **Collar:** weighted to the neck base and chest; its upper-arm weights moved to the clavicles,
  so lowered arms do not crumple it.
- **Neck skin:** a harmonic gradient from the collar (no head motion) to the jaw and skull line
  (full head motion). Gaze turns only the head bone, so the neck needs this to twist smoothly.
- **Round mouth shapes:** they use a wider, smoother envelope and displacement smoothing, because
  his wider mouth creased the decimated cheeks.
- **Mouth cavity:** clamped behind the inner lip walls; the clamp rays had passed through the lip
  slit.

**Voice and name.**

- Previously every companion spoke with Microsoft Zira and the picker promised "appearance only".
  A male companion with a female voice would break the experience.
- `CharacterOption.voice` (`female` or `male`) and `CharacterSpec.Voice` now select the voice.
- `TalkingCharacter` sends `voice` and the companion `name` with each turn.
- The local service accepts only those keys, maps them to installed desktop voices (Zira, David)
  and adds "Your name is <Name>." to the system prompt. The name must be a single roster word, so
  it can't carry instructions.
- The picker tooltip now says appearance and voice change while the chat and draft stay.

**Checks generalised.**

- The wardrobe check no longer assumes earrings exist.
- The spring floor is 16 joints, and each joint must still match `rig.json`.
- The trim message is generic.
- The in-app check verifies each companion's voice.
- Single-outfit rigs label the top colour "Top color"; "Top / dress" stays for Alita's
  outfit choices.

**Verification.** All passed:

- Arjun: 106 rig, 27 in-app and 17 face checks (67 face checks across the roster).
- Service: 16 boundary checks; a direct male turn answered as Arjun at a median pitch of 93 Hz.
- Live Arjun turn with face captures, and a 29 s review video with his voice.
- Regressions: Meera and Tara rig 105/106 and in-app 27/27, Alita meshes 143, face 15, body
  idle 34, conversation polish 56, new chat 14, replay 11, transparent chat 22, wardrobe 8,
  skin tone 24, real conversation 19, speech timing 11, speech motion 19.

**Limits.**

- Generated (not sculpted) shapes.
- Multiplicative wardrobe tints read as darker shades on his dark shirt.
- David is US-English desktop TTS, not a final voice.
- Four loaded rigs and no device or performance evidence.
- Rights are unconfirmed.

## ADR-073 — Articulated speech face and lip-only mouth shapes for all companions (2026-10-09)

The owner asked for more accurate, realistic lip-sync and expressions while the companions
respond. The focus was Meera and Tara, with Alita improved and polished too. Supersedes the
two-cue viseme crossfade and the shared 16° jaw. Extends ADR-071/072. Alita stays the scene
default and Q-016 stays open.

**Runtime (shared, per-character tuning).**

- `SpeechMouthMotion`:
  - Builds a timeline from SAPI cue durations (diphthong halves share a timestamp).
  - Dominance coarticulation in lip, jaw and tongue groups, with anticipatory rounding.
  - Guaranteed p/b/m closure and f/v lip-to-teeth contact.
  - Per-clip loudness envelope for jaw, opening, emphasis and pauses.
  - Exact critically damped smoothing in 2 ms substeps, so motion is identical at any frame
    rate.
  - Lip/jaw look-ahead and one DSP buffer of latency compensation.
- `CompanionFace`, layered on the mouth:
  - Jaw bone angle and lip seal.
  - Emotion onset, linger and fade; Duchenne smile.
  - Emotion, question and emphasis brows.
  - Speech-paced blinks with asymmetric timing.
  - Thinking look-aside and subtle head motion; Reduce motion keeps the head still.
- `CharacterOption.face` (`FaceTuning`) holds each character's jaw degrees and gains.
  - Presets: `FaceTunings.Alita()` 9.5° and `Tripo()` 11°.
  - The import copies the preset onto the roster; `Apply Face Tuning` re-syncs from code.

**Tripo rigs (Meera, Tara).**

- Rigs: re-exported with GUIDs kept.
  - Their stage-09 shapes baked a jaw rotation into every viseme.
  - The jaw weights jumped at the mouth corners, so every sound opened as the same box or slit.
- New skill stage `12_face_polish.py` (live Blender):
  - Corner-aware harmonic jaw weights.
  - Visemes and the ARKit mouth set rebuilt as lip-only postures; the bone opens the jaw and
    `mouthClose` seals over the gap.
  - Tongue and lower incisors re-placed; teeth arches rebuilt.
  - 256 px mouth atlas with painted teeth.
- Unity imports blendshape normals as None: calculated normals flipped on the thin lip walls.

**Alita.** The rig and rendering stay unchanged. Her exact CC source `Cheek_Raise` and
`Eye_Squint` channels are added to the runtime meshes so happy replies get a Duchenne smile.
This costs about 11 MB on the non-LFS body asset (now 66.7 MB) and about 2.6 MB of memory.

**Verification.** All passed:

- New suites: 19 speech-motion checks and 50 face checks.
- Rigs and in-app: Meera rig 105, Tara rig 106, both in-app 26.
- Alita regressions: runtime meshes 143, face 15, body idle 34, conversation polish 56, new
  chat 14, replay 11, transparent chat 22, wardrobe 8, skin tone 24.
- Real conversation 19, speech timing 11.
- One live local turn per character: first audio 0.64–0.69 s.
- A frame-exact review video and before/after renders are in
  `docs/evidence/m1/face-performance/`.

**Limits.**

- Shapes are generated, not sculpted; small corner artifacts remain at oblique angles.
- Lip-sync assumes the Windows SAPI viseme stream.
- AV sync was not measured acoustically.
- No device or performance evidence.
- The in-app face is about 87 px tall.

## ADR-072 — Tara: third rigged character and a generic character pipeline (2026-10-09)

Owner supplied a second Tripo GLB (one fused 1.9M-triangle surface, no skeleton or morphs)
and asked for the same treatment as Meera: rig with cloth, hair and earring physics, IK and a
face rig with blendshapes, then add her to the app with the existing features. The owner gave
no name; "Tara" is a placeholder and only appears in asset names and the roster entry.
Supersedes nothing: Alita stays the scene default, Meera unchanged, Q-016 stays open.

Rig follows ADR-071 and the project skill (`.claude/skills/tripo-character-rig`): 78k-triangle
body, CC_Base deform skeleton (108 bones incl. 50 spring-chain bones), Blender-only IK/look
controls, rotatable eyeballs with eyelid shells, cut lips with mouth interior, 76 body channels
(10 CC visemes, app expressions with 25/50/75/100 blink frames, all 52 FACE-01 channels). New
for this model: hair and tunic back are one fused surface, so hair is separated by counting
surface layers along a ray toward the torso axis rather than by colour; the beige-painted
jeans back is tone-mapped to the front denim; a Trim slot (beads, lacing, tassel) is excluded
from wardrobe tints; she is barefoot, so there is no shoe role. The jaw field is limited to face
skin above the neck after the first rig check caught it moving front lacing (285 mm at 60%).

Code: adding a third character generalizes the Meera-only editor code instead of copying it.
`CharacterSetup.Import(CharacterSpec)` and `CharacterRigChecks.Rig/InApp(spec)` hold the shared
import and validation; Meera and Tara are small specs with their existing/new menu items. The
blink-frame postprocessor keeps its class name and version (no forced reimport) but now matches
any `Imported/<Name>/<Name>.fbx` with a `<Name>.rig.json`. Wardrobe roles use material-slot
suffixes (`_Skin`, `_Top|_Kurti|_Dress`, `_Bottom|_Palazzo|_Skirt|_Pants`, `_Hair`, `_Shoes`);
Alita's renderer-name rules are unchanged. Roster checks assert Alita first and every entry
animatable instead of an exact count.

Verification: Tara 105 rig + 26 in-app checks and three live local speech turns; Meera 104 + 26
and the Alita regression suites passed after the refactor. Limits: generated (not sculpted)
shapes, spring-bone hair rather than strand simulation, recoloured denim back, three loaded rigs
with no mobile memory/performance evidence, and commercial rights for the Tripo output (Q-010).

## ADR-071 — Meera: Blender-rigged second appearance on the existing CC systems (2026-10-08)

Owner supplied a Tripo-generated GLB (one fused 2M-triangle surface, no skeleton or
morphs) and asked for a rigged, physics-animated, face-rigged character beside Alita
"reusing any existing rig or system". Supersedes ADR-043's Alita-only roster for the
local prototype; Alita stays the scene default. Q-016 production identity/roster stays open.

Rig in the live Blender session (`models/meera/Meera_Rig.blend`): reduce to 81k body
triangles (face, hands, eyes kept denser), split texture/position regions into six
role material slots on one shared atlas, and build a CC_Base-named deform skeleton so
CompanionBodyIdle (analytic leg IK, 30 finger joints, gestures), CompanionGaze and
TalkingCharacter's jaw/viseme code run unchanged. The jaw keeps the CC local axis
convention (verified: the app's negative local-Z rotation opens the chin). Blender also
holds animator-only leg/arm IK, poles, a look target and bone collections; those
controls are not exported. Bone-heat weights are overridden for jaw (harmonic field),
wider shoulder blend, hair resting on the body, earrings, kurti panels and sleeve bells.

Face: painted eye bulges replaced by rotatable eyeballs with a generated matched iris
texture (real-time highlight), socket walls, and hidden eyelid shells that rotate over
the eyeball; blink ships with 25/50/75/100 in-between frames so the lid follows the arc.
Lips are cut along the measured contact line with inner walls, cavity, teeth and tongue.
Procedural blendshapes cover the 10 CC visemes, the app's expression names and all 52
FACE-01 ARKit-style channels. These are generated deformations, not sculpted art.

Physics: hair (7 chains), earrings, six kurti panels and bell sleeves use the new
CompanionSecondaryMotion verlet springs with body sphere/capsule colliders, anchored
through bind poses and stepped by TalkingCharacter after body/face posing, paused with
the app, frozen by Reduce idle motion. Blender cloth simulation is not used at runtime.

App: Settings gains a Companion picker (appearance only; chat, draft, voice and audio
clock unchanged) remembered per device; names, greeting and wardrobe title follow the
selection. Wardrobe roles become material-slot aware with per-character saved looks;
Alita-fitted separates attach only to Alita. Import is re-runnable (Companion/Characters/
Import Meera). MeeraModelPostprocessor folds Blender `<shape>__fNN` helpers into blink frames
at FBX import and drops normal/tangent deltas on unmoved vertices, so frames stay sparse and
live in the Library. A text runtime-mesh asset in Assets was rejected: it serialized to 642 MB,
and refreshing it with CopySerialized broke GPU skinning (body not drawn).

Limits: Tripo back-side texture is weaker (hair back recoloured; kurti back faded),
generated shapes lack artist sculpt polish, and no mobile device/performance evidence
exists. Shipping requires the owner's commercial rights for the Tripo output (Q-010).



P02 adds a separate synthetic write adapter beside the history reader. A prepared
command captures immutable text plus two retry identities. Admission is reconciled
through GET /turns because its replay receipt does not prove current terminal state.
Cancel first resolves admission and reads version, then reconciles after mutation;
local Stop means transport abort, never durable cancellation or refunded usage.
Rejected credentials cannot retry until a new authenticated scope is supplied.

Composition and evidence: production/p02/README.md. 16 real Unity/API/PostgreSQL checks
passed and the full 114-group harness passed its matching-run result gate. No changes
to normal chat, current scene, provider, retention, layout, or public endpoints. Pending
commands remain memory-only; process-death persistence is P09. P03 will connect the
command/history paths to an explicitly labeled synthetic chat mode before production
identity/provider integration. This local acceptance does not close mobile/auth gates.

## ADR-069 — Sequential release backlog and local connection boundary (2026-10-08)

Owner requests sequential implementation through production with progress and memory
updated per completion. PRODUCTION_BACKLOG.md decomposes the audit into 45 subtasks.
Local verification and release verification are separate states; missing external
approval/device evidence never becomes a pass. Earlier scope and approval gates stand.

P01 extracts the local Editor credential reader from TalkingCharacter behind a source
interface. Immutable validated connections restrict the development protocol to canonical
loopback origins; players default to unconfigured. This is a seam for future mobile
services, not a hosted adapter. Do not reuse the local protocol as production identity.
Central request construction disables redirects and bounds timeouts for readiness,
turns and transcription. Missing configuration is checked before consuming a draft,
adding a user bubble or starting microphone capture. Existing local streaming remains.

18 contract, 14 runtime boundary and 14 actual streaming checks passed. Evidence at
production/p01 records the 31.244s first-text observation as a latency shortfall, not a
performance pass. Mobile compile/device gates remain open. No runtime package, scene,
existing asset GUID, Editor layout or release configuration changed. Exact pinned .NET
SDK restored locally under ignored artifacts because the system SDK had changed.

## ADR-068 — Evidence-based Google Play readiness tracking (2026-10-08)

Owner requests requirements/progress review through Play Store release. Use
PLAY_STORE_READINESS.md and the current STATUS milestone table to separate local
implementation, device verification and public-release gates. Retain historical
results but do not sum repeated tests or assign an arbitrary completion percentage.
September's diagnostic APK is not evidence for the current chat app or a signed AAB.

Android is this audit's distribution focus; iOS remains product scope but is not a
Google Play upload prerequisite. No P0 feature is silently waived, and optional P1
reminders are not elevated into a public-launch blocker. Prior owner decisions for
Alita, supplied-asset rights, guest/account separation and full-screen chat supersede
older snapshots. Physical testing remains deferred until resumed by the owner.

Official Google rules were checked and linked in the audit. No account type, provider,
budget, retention, deployment or release approval was inferred. Documentation-only
review: existing runtime evidence was inspected, not rerun. Next engineering priority
is integrated mobile service boundaries and durable normal chat with safe test adapters;
external configuration remains dependent on recorded product/provider decisions.

## ADR-067 — Clean transparent-chat text rendering (2026-10-08)

The reported broken/pixelated letters came from the dark outline styling introduced
in ADR-066. Same-resolution A/B captures with only outlines disabled restore solid
glyphs; Simulator downsampling amplifies the artifact. This screen uses UI Toolkit /
TextCore with SDFAA font assets, not TextMeshPro components. Keep existing fonts,
atlases, panel scaling and transparent surfaces. Remove message/meta outlines and use
a zero outline plus a soft 0px/1px/2px shadow on drawer text for background contrast.

Verified saved styling visually at 1170x2532 and 390x844; runtime message outline is
zero. Evidence: docs/evidence/text-rendering. No device build or physical readability
certification. Editor was stopped on resumption and restored stopped; panel asset,
scene, portrait orientation and Editor layout preserved.

## ADR-066 — Transparent full-screen conversation over a stable scene (2026-10-07)

Owner replaces the separate avatar/chat split with transparent chat covering all space
below the top navigation. Stage is now a sibling behind the UI shell, anchored to the
viewport and safe area. Chat fills below the fixed nav; keyboard insets affect only the
chat/composer. Camera fit depends on scene viewport, never message count, typing, chat
height or opening wardrobe. Actual viewport changes still reframe for full-body visibility.

Remove obsolete expand/collapse toolbar and Back behavior. Keep the transcript scrollable,
latest-message action and explicit scrollback preservation. Geometry changes follow the
latest message only while the reader is following. Default message surfaces, drawer and
composer/buttons have transparent fills, with fine borders, mint outgoing text and dark
text outlines for contrast. Existing explicit Reduce transparency preference can still
add readable message/field surfaces; navigation and modal/wardrobe surfaces stay glassy.

Acceptance updated in ConversationPolishChecks: the stage overlaps the chat instead of
occupying a separate area above it. This is the owner's requested design change, not a
waiver of full-body, portrait, keyboard or interaction checks. Physical touch/IME and
accessibility readability remain device QA; Editor captures cannot approve them.

Verification: 22 overlay, 56 portrait, 14 real streaming and 19 lifecycle checks passed
(111 total). Sparse conversation, scrollback, keyboard and compact large-text captures
reviewed. Initial/final Play=False; scene clean, portrait preserved.

## ADR-065 — Progressive local text/voice and truthful AI activity (2026-10-07)

Owner requested streaming text/voice and WhatsApp-like typing, online and last-seen.
The local Ollama request now uses streaming structured output. A bounded incremental
JSON decoder emits only decoded text deltas (including split escapes/Unicode), followed
by exact canonical text/emotion. Complete sentences queue Windows speech while inference
continues; at most three speech jobs run sequentially. Audio/cues arrive as each job
finishes, with independent ordered delta/audio sequences and a final count. Abort cancels
inference/synthesis; malformed final output remains a failure, never committed context.
This is local development streaming, not production provider or streaming moderation.

Unity grows one reply bubble, validates canonical text against received deltas, starts
queued audio before stream completion, and keeps existing audio-clock facial motion,
Stop/Retry/Replay behavior. Framing queue supports bounded token bursts. Conversation
following is driven by user scroll intent; incoming text does not misclassify layout
growth as scrolling away. Active conversation has more readable transcript space.

Header explicitly says Local AI: Online follows a successful readiness/response check,
Typing follows generation, Speaking follows playback, and Last seen is the last successful
availability observation in this session. 30-second foreground idle checks and 45-second
freshness prevent indefinitely stale Online. No human-presence/read-receipt fiction or
persistent activity tracking. Typing dots respect reduced-motion preference.

12 deterministic streaming tests, 13 real service checks, 14 live Unity streaming/activity
checks and 11 real replay checks passed. One warm Unity observation: first text 0.078s,
first audio 0.769s, stream complete 1.332s, 14 distinct partial text states. Another local
service run: 2.279s / 2.799s / 3.200s. These are observations, not latency guarantees.
Native mobile transport, network/device latency and approved providers remain open.

## ADR-064 — Gesture-driven microphone permission recovery (2026-10-07)

UNITY-03 partial: gate capture behind an injectable IMicrophonePermission adapter.
Android uses Permission.RequestUserPermission; iOS uses RequestUserAuthorization.
A first unavailable-permission mic tap opens an explanation, Continue requests OS
permission, and grant requires a fresh mic tap. Denial offers retry, explicit device
settings and Not now. Back/pause/disable cancel the UI coroutine; late callbacks never
start capture. Check permission again before capture and while recording. Desktop
retains existing capture behavior and reports device/OS errors with platform-neutral copy.

Generated Android manifests gain RECORD_AUDIO and SkipPermissionsDialog=true without
replacing activities; transformation is idempotent. iOS usage description is configured
and builds reject an empty purpose. These native branches are implemented but not yet
validated in an Android/iOS build. No Editor target/layout switch or build was performed.
Audio-configuration callbacks stop current work with recoverable headset/mic guidance.
This does not implement full native audio-focus/session routing or phone-call detection.

16 Editor checks cover fake permission denial/grant/pending cancellation, explicit
settings, no automatic recording, Unicode draft preservation, compact touch targets,
silent playback interruption and manifest transformation. Native prompts/settings,
permission revocation with live capture, Bluetooth and phone calls require device QA.
The local Windows inference/transcription adapter is still not a mobile speech service.

References: [Unity permission callbacks](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Android.PermissionCallbacks.html),
[Unity iOS purpose](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.iOS-microphoneUsageDescription.html),
[Unity Android permissions](https://docs.unity.com/en-us/engine/6000.7/manual/platform-specific/android/developing/device-features-and-permissions/permissions-in-unity).

## ADR-063 — Mobile pause recovery and contextual Back (2026-10-07)

Advance UNITY-02/UNITY-03 independently of production identity/provider decisions.
TalkingCharacter now owns an idempotent suspended state. OS pause cancels capture,
reply/setup request ownership and speech, stops the history listener, freezes procedural
updates and disables its portrait camera. Resume restores the camera's previous enabled
state and offers a usable draft; it never restarts microphone, speech or network work.
Mobile/standalone no longer force Application.runInBackground on; Editor keeps its local
evaluation behavior. Disable restores the original camera/background settings.

UI Toolkit navigation-cancel closes the topmost history/settings/wardrobe surface,
rolls back an unsaved wardrobe preview, stops recording or collapses expanded chat.
At the root it leaves host navigation unconsumed and preserves the conversation.
Callbacks are registered once per view rebuild. Android native Back/IME ordering still
requires a physical build; this is not evidence of predictive-back integration.

20 Editor lifecycle/navigation checks passed, including 20 repeated cycles, silent
fixture playback interruption, blocked suspended actions, frozen body and Unicode draft.
56 portrait regressions and 11 actual local speech/replay checks passed. Initial speech
run failed because Ollama was stopped; restarting the existing installed service resolved
it. No physical microphone, audio-focus, process-death,
permission-revocation, native keyboard or IL2CPP evidence is inferred. Drafts remain
session-only; approved retention/encryption must precede disk persistence.

## ADR-062 — Local skin-tone customization (2026-10-07)

Style offers Original, Light, Medium, Tan, Brown and Deep swatches with live preview.
Save persists the tone with the existing local wardrobe; Cancel restores the complete
previous look. Restore signature look returns skin to Original. Older saved JSON
without skinTone defaults to Original; invalid indices also fall back to Original.

Tint runtime clones of the four authored head/body/arm/leg materials together.
Preserve base textures, normals and alpha; leave eyes, teeth, nails, lashes, hair and
clothing alone. Reuse those clones across changes and restore material references on
disposal. No source material, scene, asset GUID, shader or provider change.

13 material checks and 24 live UI checks passed, including persistence/reload, rollback,
44px targets and compact portrait bounds. Six face and six body renders reviewed.
Physical-device rendering and accessibility validation remain pending.

## ADR-061 — Varied full-body idle gestures with contact constraints (2026-10-06)

Owner requests subtle foot movement, hip turns, stretches and yawns with both hands
raised, without an obvious repeated loop. Preserve the original relaxed/finger baseline
as a deterministic sampler and add a session-local scheduler around it. Five gestures:
foot adjustment, hip turn, shoulder roll, side stretch and two-arm yawn. The scheduler
excludes the two most recent gestures, varies side/amplitude/duration, inserts 6–15 s
rest intervals, and uses 80 s yawn / 35 s side-stretch cooldowns. Baseline breathing phase
also varies slowly. This avoids a fixed repeating playlist; it does not imply infinitely
unique animation or motion-captured realism.

Foot adjustments transfer weight before lift, travel about 6 cm with sole clearance,
settle, lift and return. Analytic leg IK preserves the support foot and finishes foot
landings during conversation. Larger gestures blend out over 1.25 seconds and no new
decorative gesture starts while listening, generating or speaking. Speech takes the jaw
and face immediately; yawns coordinate two raised arms, soft fingers, eye closure, jaw
opening and a slight head tilt. Reduce idle motion restores the stationary relaxed pose
while retaining normal blinks. Cached imported rig poses restore on disable.

Camera bounds include the raised-arm envelope once, avoiding clipping or continual
zoom changes. Sources, scene, asset GUIDs, portrait orientation and Editor layout remain
unchanged. No Animator dependency or external animation download is introduced.

Final verification: 29 expressive-motion checks (contact, continuity, endpoint joins,
10-minute scheduling, interruption, reduced motion, restoration) and 34 baseline checks
passed. 75 baked outfit/gesture/angle renders inspected; five live gesture/framing checks and 56 chat-layout checks passed.
A timestamped 46.5-second actual app capture shows the transitions. 11 real local speech/replay checks passed (135 checks total). Editor restored to stopped,
scene clean, portrait preserved, Console errors/warnings empty. Existing outfits reviewed at raised-arm extremes. Physical-device
performance and broader locomotion remain unverified.


## ADR-060 — Local Alita wardrobe and attention-driven gaze (2026-10-06)

Owner requests separate upper/lower clothes, other appearance options, and a less robotic
idle. Add a local Style drawer with original dress or four mix-and-match combinations:
relaxed tee / sleeveless shell × denim shorts / midi skirt. Cloth, hair and sneaker tints
are independent. Preview changes are temporary; Cancel restores the entire prior look,
Save persists a versioned local loadout. Turn preview orbits the character camera without
changing the character root, scene asset or Editor Game-view selection.

Use the owner's supplied Cosmos tee/shorts, fitted in the existing interactive Blender
PID to Alita using their identical body topology. Derive shell/skirt from Alita's dress.
Blender source and named-weight/UV/normal exports live in models/wardrobe; existing source
FBXs remain unchanged. WardrobeImport converts handedness and maps the existing Alita
bind poses, preserving generated asset GUIDs on subsequent imports. Runtime uses four
cached garment renderers and instance materials; disable restores original materials.
No arbitrary body sliders are exposed: the supported fit is this exact Alita body.

CompanionGaze adds irregularly spaced, held room glances, eyes leading the head, blinks
around some transitions and a smooth return toward the viewer while recording, waiting
for a response or speaking. The existing 24-second planted-foot/finger idle continues.
Gaze uses model-space axes and restores imported local rotations on disposal.

Verification uses live portrait UI and baked pose captures. Rapid sequential Camera.Render
calls with live skinned meshes showed stale skin matrices; the isolated review now bakes
each sampled pose before rendering. Do not treat earlier unbaked captures as evidence.
Three views × three poses × four combinations were generated and inspected as contact
sheets. 34 body, 3 outfit/gaze, 8 live UI, 56 chat-layout and 11 real speech/replay checks
passed (112 total). A 44-second live app recording verifies motion. Ollama was stopped
initially; started its existing installed local server and reran speech successfully.

This advances local WARD-01 preview only, not M4 completion. No inventory, purchase,
server equip/ownership, remote catalog, body-shape compatibility or mobile performance
claims. These remain separate shipping requirements. Appearance preferences are local
cosmetics and do not alter conversation retention.


## ADR-059 — Articulated hands and reviewed idle cycle (2026-10-06)

Owner rejected ADR-058's straight, unanimated fingers and requested a visually reviewed
result. Body movement alone was insufficient. Extend CompanionBodyIdle to own all 30
finger/thumb joints, with anatomical curl axes derived from the mirrored palm geometry,
progressively deeper curl from index to pinky, and thumb opposition towards the index.
Each joint restores from its original local pose before sampling. Thumb placement was
revised after front/side close-up review showed excessive splay in the first iteration.

Use a coordinated 24-second body/hand cycle with five breaths, a slower asymmetric weight
shift, soft wrist extension/roll and staggered finger release/settle. Avoid separate
unrelated sine frequencies that cannot return to a common loop boundary. Retain planted
feet, unchanged facial ownership and the reduced-motion relaxed pose. Preserve source
assets, rig proportions and scene GUIDs.

Visual evidence includes both hands from front/side at 0/6/12/18 seconds, a full-cycle
diagnostic hand video sampled by the actual pose function, and a separate live app preview.
Technical tests verify all digits move and the 24-second loop closes, but do not substitute
for visual review. This is still procedural animation on the supplied rig, not mocap.

## ADR-058 — Procedural relaxed body idle with planted feet (2026-10-06)

Owner reports the full-body avatar remains in an A-pose with motion only above the neck.
Inspection confirms no Animator/controller or body clips on the current rig. Replace
the fixed 65-degree arm lowering with CompanionBodyIdle, a deterministic procedural
layer owned by TalkingCharacter. It binds the existing CC skeleton; no model, animation
download, source geometry, scene or GUID replacement is required.

Pose includes lowered arms, soft elbows, shoulder breathing, spine/chest movement,
asynchronous arm/wrist settling and hip weight shifts. Analytic two-bone leg solves hold
both ankle positions and foot rotations at their starting anchors. The character root
never translates. Cache original local transforms and restore on disposal/character
switch; absolute-time sampling prevents accumulated drift. Missing body chains produce
a warning. Body animation never writes head, jaw or facial blendshapes, preserving the
existing audio-clock facial path. Full-body framing caches the relaxed pose with margin.

Settings adds Reduce idle motion: keep the relaxed pose, disable decorative body/head
sway while retaining speech and blinks. This is an original procedural idle, not a
retargeted mocap clip or a general walking/gesture system. Ground anchors assume the
existing flat stationary room. More expressive gestures and other outfits need separate
art/rig review; physical mobile performance remains unverified.

## ADR-057 — Native glass chat and adaptive full-body rendering (2026-10-05)

Owner authorized implementation of ADR-056. TalkingCharacter is now partial: conversation
and speech remain in the original file; TalkingCharacter.View.cs owns presentation and
Resources/CompanionUI/Conversation.uss holds visual tokens/styles. No scene or existing
asset GUID replacement. The generated room plate is imported with its own metadata,
compressed, mipmaps disabled and max texture size 2048. Existing Alita/outfit is preserved.

Bake posed skinned geometry once to derive actual full-body bounds (imported bounds still
include T-pose arms). Fit a transparent 4x-MSAA render texture to the available stage,
including aspect and depth margins. Bounded texture resolution follows stage geometry.
Camera configuration is restored on disable. Initial unresolved/NaN layout is ignored.
This is flat room compositing with a simple UI contact shadow, not a 3D environment.

UI uses tinted glass without a blur pass; reduced transparency and 150% message text are
local preferences. Keyboard/drawer changes reframe the whole body. Settings is modal,
blocks underlying chat interaction and houses local setup/microphone/history diagnostics.
New chat confirms discard in the UI. Native vector mic/send/settings/expand icons avoid
font glyph dependencies. Stop/Retry/Replay appear only when useful; retry reuses the existing
user bubble. Message timestamps do not imply human delivery/read receipts.

Recording uses the existing real local capture level/timer and explicit finish/review flow.
No realtime calling, Hindi speech, production identity, storage policy or provider changes.
Native device keyboard/accessibility and render budgets remain unverified; the presentation
work advances PROD-01/03 and UNITY-02/03 without completing those requirements.

## ADR-056 — Full-body companion and glass conversation design (2026-10-05)

Owner requests production UI planning, full-body Alita and transparent messaging overlays;
explicitly selects warm evening room, emerald accents, smoky glass. Preserve the existing
Alita model/outfit/GUIDs. Use an immersive environment layer with native overlay controls,
adaptive full-body camera fitting and an expandable conversation drawer. Keyboard/large
text reduce the stage and reframe the character; focused transcript is an explicit mode.

The detailed proposal is [production UI plan](design/production-ui/PLAN.md). Two built-in
image_gen outputs are saved with exact prompts. They are design/candidate artwork, not
implemented UI or proof of device quality. Incidental double ticks in the concept must
not become human read receipts. Mic continues explicit record/review/send until actual
live voice is integrated. No generated outfit replacement is authorized by the concept.

Start glass with tint/scrim and bounded opacity; shared blur is a measured follow-up, not
an assumed UI Toolkit feature. Keep reduced-transparency/motion and accessible text states.
Move development controls into identified development settings without losing diagnostics.
Current session-only/local adapter and production identity/provider/data gates remain.
This turn changes design documentation/assets only; runtime implementation is next.

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

## ADR-049 - Unity-compatible synthetic history transport (2026-10-05)

AccountHistory/AccountEventDecoder live in the existing engine-independent Core assembly;
DTOs use Unity-compatible fields and the decoder accepts a serialization function. This
avoids loading the .NET 10 client or adding a JSON package to Unity. A separate presentation
adapter uses JsonUtility and UnityWebRequest, bounded UTF-8 SSE frames, Last-Event-ID,
serialized operations, three reconnects with backoff/jitter, and explicit Stop/Dispose.
One adapter owns one immutable synthetic endpoint/credential/conversation scope. No disk
storage or automatic scene attachment. Release builds reject the local adapter; Editor
and development builds still require explicit syntheticOnly and a loopback endpoint.

Load hydrates event history; Follow resumes from the applied cursor. EOF does not complete
a turn. Stop aborts the local connection, not the durable backend turn. Error strings are
fixed codes without tokens/payloads. Consumers read detached snapshots on Unity's main
thread. The .NET client remains a separate protocol reference; divergence is a maintenance
risk covered by equivalent replay/fragmentation tests until shared packaging is consolidated.

Nine checks passed in Unity 6000.5.9f1 while stopped, using actual loopback HTTP sockets:
fragmented Unicode, partial-frame checkpoint, duplicate convergence, detached snapshots,
endpoint guard, hydration, reconnect, Stop, and applied cursor headers. No scene/layout/
Game-view change, package upgrade or Play-mode transition. Actual account API from Unity,
portrait account/history UI, IL2CPP and physical network behavior remain unverified.

## ADR-050 - Opt-in synthetic history screen and real API Editor bridge (2026-10-05)

SyntheticHistoryView is a reusable UI Toolkit view with a single vertical column, maximum
width 480, flexible transcript scrolling and 44px minimum controls. It displays an explicit
synthetic/local banner, loading/live/reconnecting/recovery states, Stop and Connect/Retry.
It renders canonical turn state and never invents an assistant bubble for cancellation.
Stop ends listening only. Reconfiguration disposes the old transport and clears account
history; retry keeps the applied cursor. Credentials are not exposed through text fields.

An explicit Companion/Open Synthetic Account History menu hosts the view in an Editor
window; the agent does not open it automatically or change the existing layout. This is
an integration lab screen, not yet a mobile app route. The talking scene stays session-only.
The common view is independent of Editor APIs so a future runtime host can reuse it.

The disposable database/API harness supports --unity. It writes only the loopback endpoint,
ephemeral synthetic bearer identities and conversation to an ignored fixture, waits up to
five minutes for matching run-id evidence, then removes credentials in finally. The Editor
check drives the real view against PostgreSQL-backed JSON/SSE without entering Play mode
or opening any window. The fixture contains no database connection string or provider key.
No real-user collection or production identity/retention decision is implied.

Verification: eight actual API/Editor view checks, ten socket/transport checks and 114
full harness groups passed. The first integration attempt exposed JsonUtility converting
JSON null strings to empty strings. Normalize absent cancelled/failed text at the Unity
serialization boundary, leaving the core canonical null invariant intact. Regression
coverage now exercises this behavior. No window was opened and no visual layout/pixel
verification is claimed; host routing and mobile lifecycle remain next.

## ADR-051 - Development runtime history navigation (2026-10-05)

The talking screen exposes History lab only in Editor/development builds. Navigation
interrupts existing speech/recording before hiding current chat elements and mounting the
synthetic history view. Back disposes the history transport and restores the same chat
objects and display settings; no draft/transcript is copied to the account API. Pause
stops history listening, and component disable disposes navigation before clearing UI.
Insets follow the portrait shell. A missing fixture shows setup guidance with disabled
connect controls, not fake history or a fabricated connection.

Editor Play can load the ignored short-lived synthetic fixture on explicit navigation.
No fixture is bundled, and a standalone development build currently shows setup guidance:
its secure configuration delivery is not implemented. Release builds expose no lab route.

Runtime layout evidence uses a temporary additive scene and RenderTexture with a cloned
PanelSettings, never the Game-view size/selection. The first stopped-Editor attempt had
no resolved runtime panel; the test therefore requires Play. Thirteen checks passed at
360x640 and 390x844 with 24px simulated top/bottom insets. Captures were visually inspected;
they show the missing-fixture state, not a populated conversation. Play was restored to
stopped and the active TalkingCompanion scene remained clean with its original five roots.

## ADR-052 - Populated runtime history and larger message text (2026-10-05)

The synthetic history view adds a session-only Larger messages toggle. It changes message
body text from 16 to 24px while leaving navigation and disclosure legible and fixed outside
the scrolling transcript. This is not a claim to implement OS-wide font scaling or full
accessibility. Reflow uses the canonical in-memory history; no requests, admissions or
storage changes are triggered by the preference.

The disposable harness supports --unity-runtime, seeds six synthetic completed turns
with long replies in a separate conversation, and publishes its ID only in the ignored
fixture. Existing ownership/cancellation fixtures remain separate. These are synthetic
SQL lifecycle fixtures, not real provider calls or a production quota bypass. Runtime
navigation selects the supplied runtime conversation when available. Matching run-id
completion still gates cleanup and test success.

The new Play-mode check exercises the actual API via runtime navigation on a 360x640
RenderTexture, including scroll overflow, larger text, fixed action bounds, reaching the
last reply, pause and Back. It does not change Game-view configuration or window layout.

Verification: 16 populated-runtime checks and the full 114-group harness passed. Normal,
150% message body and scroll-bottom captures were visually inspected. Stop/Back retained
and then cleaned up canonical history correctly. Temporary scene/resources and credential
fixture cleaned up; stopped Play state and clean TalkingCompanion restored. Physical
input, OS scaling, screen readers and Hindi font rendering remain unverified.

## ADR-053 - Runtime history recovery distinguishes authentication from outages (2026-10-05)

The Unity transport classifies 401/403 as requiring session reload, and 400/404 or invalid
history/stream data as non-retryable with the same local configuration. The view disables
Connect/Retry for those states and guards the action itself, so calling Connect or Stop
cannot bypass the gate. Stop retains recovery guidance when an error exists. A fresh
Configure disposes the old transport and clears its projection before rehydration.

Temporary network/503/429 behavior retains the existing bounded reconnect policy. After
exhaustion the view retains loaded history and offers explicit Retry. Stream body receipt
only indicates Live when the response status is 200; an error body cannot briefly label
the connection as live. No admission/provider work is retried by any of these actions.

The new Play-mode socket fixture drives the runtime view through a partial terminal frame,
duplicate replay, a controlled 401 response, a newly configured session, and four 503
stream responses. Controlled HTTP faults are deterministic simulations, not an external
identity provider or physical mobile-network test. Backend clock-expiry evidence remains
separate from the runtime response-handling checks.

Verification: 12 runtime fault cases passed, expiry and transient-outage captures inspected;
10 adapter/socket checks and eight real-API Editor checks rerun green. Full disposable
--api --unity suite passed 114 groups. Initial/final Play state stopped, scene clean,
Console errors empty, no Editor layout/Game-view changes or production configuration.

## ADR-054 - Retained history cards and keyboard transcript access (2026-10-05)

SyntheticHistoryView retains one card per canonical turn and updates its text only when
the turn version changes. Larger-message reflow updates existing font sizes. Account
reconfiguration explicitly clears the row map and transcript. RenderedText is now an
on-demand StringBuilder diagnostic rather than repeated concatenation of all history on
every render. This avoids resetting scroll/focus and rebuilding unchanged UI elements.
The existing 1000-turn session-only cap remains unchanged; this is not a retention policy.

The transcript is keyboard-focusable, has a visible cyan focus outline, and supports
Home/End/Page Up/Page Down. These are keyboard improvements, not screen-reader/OS-scale
certification. No backend requests are issued by keyboard navigation or reflow.

Measured before editing: three warm renders of 1000 completed synthetic turns took
125.257/113.140/71.467 ms. After retaining cards: 0.159/0.146/0.127 ms. These measurements
cover detached UI construction on this Editor, not first-paint cost, frame time, device
latency or RSS. Unity returned zero from its allocation counter in both runs, so no
allocation claim is made. Initial construction still creates all cards; virtualization
and physical-device resource bounds remain open.

Verification: 13 Play-mode scale/keyboard checks passed at 1000 turns; one-terminal update,
row reuse, scroll preservation, larger text, keyboard focus/scroll, capacity rejection and
account clearing were exercised. Twelve runtime recovery and eight actual-API view checks
rerun; full harness 114 groups green. Focus capture inspected. Play restored to stopped,
active scene clean, Console errors empty. Hardware keyboard/screen-reader checks pending.

## ADR-055 - Variable-height virtual history rows (2026-10-05)

Use Unity's installed UI Toolkit ListView with DynamicHeight virtualization for the
synthetic history screen. Wrapped replies retain their natural height while offscreen
cards are recycled. Canonical session data remains separate from UI bindings; version
changes refresh a visible item, and count/font changes refresh the list. Reconfiguration
clears both data and bindings. No new package, scene, GUID, retention or provider change.

Keyboard events and the focus outline are handled at the list boundary, including its
internal focused descendants. Home/End use ScrollToItem so estimated offscreen heights
do not prevent reaching the final message. Page navigation uses viewport height.

15 runtime scale checks verify 1000 turns with at most 32 bound cards at initial/end
positions, offscreen terminal updates, larger text, keyboard access and session clearing.
16 populated runtime checks and 114-group actual database/API harness passed. UI bounds
are not whole-process memory or frame-time measurements: snapshots still copy canonical
data and Render walks it. Prior ADR-054 timings describe the previous retained-row code.

Final regression: 12 runtime recovery checks passed. Play restored to stopped;
TalkingCompanion clean with five roots; Console errors empty; credential fixture removed.
Git diff whitespace check passed. Physical-device checks remain unexecuted.
