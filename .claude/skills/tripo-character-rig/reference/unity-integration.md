# Unity integration for a new rigged character

Unity 6000.5 project at `apps/unity`, scene `Assets/Companion/Scenes/TalkingCompanion.unity`.
Drive the Editor through the Unity MCP (`Unity_ManageEditor`, `Unity_ManageMenuItem`,
`Unity_RunCommand`, `Unity_ReadConsole`). Record the Play state first and restore it at the
end. Do not touch window layout or the Game-view size, and do not run "Portrait Preview".

## What already works for any CC_Base character (no code changes)
- `TalkingCharacter.characters` (`CharacterOption{name, model, portraitDistance, face, voice}`) roster;
  the Settings "YOUR COMPANION" dropdown (`character-picker`), remembered in PlayerPrefs
  under `Companion.Character.v1`. Index 0 is Alita.
- Name-aware header, composer placeholder, typing label, greeting ("Hi, I'm <Name>…") and
  wardrobe title.
- Per-character saved look: `CompanionWardrobe.Preference + "." + <Name>`.
- `CompanionBodyIdle` (leg IK, gestures, 30 finger joints), `CompanionGaze`, jaw and viseme
  drive, expressions: all keyed on CC_Base bone names and the blendshape contract in SKILL.md.
- Talking face (ADR-073):
  - `SpeechMouthMotion` turns TTS viseme cues and the clip loudness into channel weights.
    It uses coarticulation, p/b/m and f/v contacts, and frame-rate-independent smoothing.
  - `CompanionFace` layers the jaw bone, lip seal, emotion onset/linger, Duchenne smile,
    brows, speech-paced blinks, thinking look-aside and head motion on top.
  - Per-character gains live in `CharacterOption.face` (`FaceTuning`): jaw degrees, per-viseme
    gains, seal, smile, brow and head motion.
- Voice and name (ADR-074): `CharacterOption.voice` is `female` or `male`, set from
  `CharacterSpec.Voice` at import. Each turn sends `voice` and `name`. The local service maps the
  key to Microsoft Zira or David Desktop and tells the model the companion's name (one word).
- `CompanionSecondaryMotion`: verlet springs built from `<Name>.rig.json` and bind poses;
  reset on disable; settles under reduced motion.

## Shared character code (generalized for Tara, 2026-10-09, ADR-072)
Adding a character no longer needs editor code copies. Add one file like
`Editor/TaraCharacterSetup.cs`:
- a `CharacterSpec`: name, material slots (name, texture, smoothness, metallic, bump), the
  top/bottom/trim slot names used by the checks, `HasShoes`, and a role summary line;
- three menu items: `Import <Name>`, `Check <Name> Rig` and `Check <Name> In App`.

| File | Role |
|---|---|
| `Editor/CharacterSetup.cs` | `Import(spec)`: textures, URP materials, model settings, scene instance, springs from rig.json, roster entry with `spec.Face`. `FaceTunings.Alita()`/`Tripo()` hold the shared presets |
| `Editor/FaceTuningSetup.cs` | `Companion/Characters/Apply Face Tuning`: re-applies every spec's `FaceTuning` to the roster and saves the scene (stopped Editor) |
| `Editor/CharacterRigChecks.cs` | `Rig(spec)` (stopped, isolated copy) and `InApp(spec)` (Play); evidence goes to `docs/evidence/m1/<name>/` |
| `Editor/MeeraModelPostprocessor.cs` | Folds `__fNN` blink frames for any `Imported/<Name>/<Name>.fbx` that has `<Name>.rig.json`. Keep the class name and `GetVersion()`, or every rigged FBX reimports |
| `Presentation/CompanionWardrobe.cs` | Slot suffix roles: `_Skin`; `_Top`/`_Kurti`/`_Dress`; `_Bottom`/`_Palazzo`/`_Skirt`/`_Pants`; `_Hair`; `_Shoes`. Any other slot (`_Trim`, `_Earrings`) is never tinted. Alita's renderer-name rules unchanged |

Slot naming: only use those suffixes for slots that should follow a wardrobe colour. A
barefoot character has no `_Shoes` slot, and the wardrobe then hides the shoe picker. Checks
assert Alita is index 0 and every roster entry can animate, not an exact roster size.

## Import steps (stopped Editor, TalkingCompanion open)
1. Copy `D:/Blender/Companion_Character_Rig_<date>/export/` → `Assets/Companion/Imported/<Name>/`
   (`<Name>.fbx`, `<Name>.rig.json`, `Textures/<Name>_BaseColor.jpg` 4K, `_Normal.png`,
   `_MetallicSmoothness.png` with R=metal and A=1−roughness, `_Eyes.png`, `_Mouth.png`).
2. Run the import menu. What it does:
   - **Textures:** max sizes 2048/2048/1024/1024/256. The mouth atlas is bilinear with mips
     and uncompressed: since ADR-073 it carries painted teeth, not 16 px colour blocks.
   - **Materials:** creates URP Lit materials in `Imported/<Name>/Materials`.
   - **Model:** Generic rig, blendshapes on, normals Import, blendshape normals **None**
     (calculated ones flipped on the thin lip walls), tangents CalculateMikk, then remaps the
     materials.
   - **Blink frames:** after re-import it verifies they were folded into
     `Eye_Blink_L/R` (4 frames).
   - **Scene:** adds the prefab instance at Alita's transform, reverts any `m_Mesh`
     override, and sets `updateWhenOffscreen=false`.
   - **Springs:** builds them from rig.json. The axis mapping comes from a
     permutation/sign search with ≤2 mm error.
   - **Roster:** registers the CharacterOption, leaves the character inactive and saves the
     scene.
3. Never create a runtime mesh copy (`CopySerialized`) or a `.asset` mesh. It broke GPU
   skinning and produced a 642 MB YAML file. Blink frames are folded at import only.

## Validation
Stopped Editor:
- `Companion/Characters/Check <Name> Rig`, 105 checks on Meera, 106 on Tara and Arjun (each adds a trim check):
  - renderers and URP materials;
  - channels deform (>0.5 mm) and reset; the 52 ARKit names; blink frames;
  - facing; jaw chin test; feet planted over 30 s; fingers; gaze;
  - wardrobe roles and tints;
  - springs bind, respond, settle and stay outside colliders; reduced motion;
  - live GPU-skinned draw coverage;
  - face tuning from the spec is on the roster;
  - review renders (`ExpressiveIdleReview.Capture`, warm-up render first). Face renders are
    posed like the app, with shape + jaw bone + seal: aa, oo, ee, f/v, p/b/m and happy.
- `Companion/Characters/Run Face Performance Checks`, 67 checks with four characters (16 Alita, 17 per Tripo
  character): roles bind, blink rate and shape, smile onset/linger/fade, Duchenne cheeks,
  brows, Stop clears, head motion, jaw range, p/b/m seal, thinking look-aside. Runs on an
  isolated copy (`FaceRig`) and writes `docs/evidence/m1/face-performance/face-checks.txt`.
- `Companion/Run Speech Mouth Motion Checks`, 19 checks on `SpeechMouthMotion`: frame-rate
  independence, diphthongs, closures, f/v, anticipation, loudness, emphasis, pauses.
- `Companion/Characters/Render Face Performance Review` (optional, needs ffmpeg). Plays
  recorded fixtures (`artifacts/face-review/turn*.json`, ignored) through the runtime face on
  every roster character and writes a side-by-side `face-performance.mp4`.

Play mode (enter Play, run, exit Play):
- `Companion/Characters/Check <Name> In App`, 27 checks (26 before the voice step):
  - picker, remembered selection, draft preserved, labels, the companion's voice;
  - springs running, framing bounds;
  - portrait camera alpha coverage (Meera: 264k opaque pixels);
  - live face channels, wardrobe, cancelled preview not saved;
  - greeting; switching back releases springs.
- It restores the prefs afterwards.

Alita regressions after any shared-code change:

| Suite | How to run | Meera-era count |
|---|---|---|
| Runtime meshes | `Companion/Check Alita Runtime Meshes` | 123 (143 since ADR-073 added cheek/squint channels) |
| Face | `Companion/Check Alita Portrait and Face` | 15 |
| Body idle | `Companion/Run Full Body Idle Checks` | 34 |
| Conversation polish | `Companion/Run Conversation Polish Checks` | 56 |
| New chat | `Companion/Run New Chat Checks` | 14 |
| Reply replay | `Companion/Run Reply Replay Checks` | 11 |
| Transparent chat | `Companion.Editor.TransparentChatChecks.Run()` via `Unity_RunCommand` | 22 |
| Wardrobe UI | `Companion.Editor.WardrobeUiChecks.Run()` | 8 |
| Skin-tone UI | `Companion.Editor.SkinToneUiChecks.Run()` | 24 |

These suites call `app.SelectCharacter(0)` first. Any new suite that assumes Alita must too.

`Run Portrait Layout Checks` targets the M0 mock scene ("Mock app missing" there is
expected and harmless).

## Live speech turn
Start the local service (`Companion/Start Local Talking Service`). In Play, select the new
character and send one message. Record:
- time to first text and first audio (Meera: 6.5 s and 7.6 s);
- that visemes and the jaw are driven;
- a mid-speech frame, saved as `docs/evidence/m1/<name>/live-speech-frame.png`;
- optionally face crops: `Companion.Editor.LiveFaceCapture.Run(folder, message, index)`
  via `Unity_RunCommand` in Play. It writes 16 crops at 0.25 s plus `live-turns.txt`, then
  restores the selected character.

Never print the session file.

## Finish
- Exit Play. Leave the scene saved and Alita as the scene default. Leave the owner's
  `Companion.Character.v1` pref as you found it.
- Delete scratch objects.
- Check `git status`: new LFS files should be only under `Imported/<Name>/`,
  `models/<name>/` and the evidence folder.
