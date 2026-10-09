# Face performance: lip-sync and expressions (ADR-073, 2026-10-09)

The owner asked for more accurate, realistic lip-sync and expressions while the companions
respond. The main targets were Meera and Tara; Alita "looks decent" but was to be improved and
polished too. This folder holds the evidence. The change has three parts:

- one shared runtime face for all three characters;
- a Blender face polish of the two Tripo rigs;
- two expression channels added to Alita's runtime meshes.

## What changed

### Shared runtime (Alita, Meera, Tara)

`Core/SpeechMouthMotion.cs` was rewritten. The old version crossfaded two neighbouring cues at
fixed strength, opened the jaw to at most 4.5°, and never guaranteed lip contact.

- **Timeline:** rebuilt from each SAPI cue's duration. Windows speech sends both halves of a
  diphthong with the same timestamp, which used to collapse them into one.
- **Coarticulation:** dominance-based, with separate lip, jaw and tongue groups. Neighbouring
  sounds overlap, and rounding anticipates the sound (w/uw 110 ms, ow 90 ms ahead).
- **Contacts:** p/b/m always seal the lips, and f/v always bring the lower lip to the upper
  teeth, even for 40–60 ms cues.
- **Loudness:** a 10 ms RMS envelope of each clip scales the jaw and the opening per syllable.
  Loud peaks become emphasis pulses, and silences inside a sentence are reported as pauses.
- **Smoothing:** exact critically damped motion in 2 ms substeps, so 30, 60 and 120 FPS give
  the same curves.
- **Timing:** the lips lead the audio by 22 ms and the jaw by 28 ms. One DSP buffer of output
  latency is subtracted.

`Presentation/CompanionFace.cs` is new. It is the face layer above the mouth:

- **Jaw and seal:**
  - Jaw bone angle = articulation × `FaceTuning.jawDegrees`.
  - On rigs with `mouthClose`, the lips seal over the bone's gap during closures and rounding.
- **Emotion:**
  - Onset 0.32 s, release 0.55 s.
  - After a reply the expression lingers at half strength for 2.4 s, then fades over 4.5 s.
- **Smile:** happy replies give a Duchenne smile (corner pull plus cheek raise and squint).
  The smile is reduced while the lips round.
- **Brows:** concern lifts the inner brows, curiosity gives an asymmetric outer raise, and
  questions and emphasis lift the brows briefly.
- **Blinks:**
  - Shape: close in 75 ms, hold 30 ms, open in 160 ms.
  - Timing: more often while talking, at pauses and at sentence starts.
  - Variety: occasional doubles and partial blinks.
- **Thinking:** she looks up and away while the reply is generating, and glances aside at
  some sentence starts.
- **Head:** small speech sway, emphasis nods, and a head tilt for questions and curiosity.
  Reduce motion keeps the head still.

`CompanionGaze` adds attention-scaled micro-saccades and accepts the look-aside.

`TalkingCharacter` holds a per-character `FaceTuning` on each roster entry and passes the cue
durations, clip envelope and question flag. Stop clears the face at once.

### Meera and Tara rigs (live Blender session)

The skill stage `blender/12_face_polish.py` re-exports both FBXs; asset GUIDs are unchanged.

- **Jaw skin weights:** a harmonic solve, corner-aware. The lip halves meet at 0.5 at the
  corners, so the jaw opens an oval instead of a box.
- **Mouth shapes:** the 10 visemes and the ARKit mouth set were rebuilt as lip-only postures.
  - Round, protruded oo/w; spread ee; sealed p/b/m; lower lip to teeth for f/v; flared sh.
  - The app's jaw bone now does all the opening.
- **Mouth interior:**
  - The tongue was shortened and placed behind the lower incisors (it showed through Tara's
    chin).
  - The lower incisors sit just behind the uppers.
  - Both teeth arches were rebuilt wider, with premolars.
- **Mouth atlas:** 256 px with painted teeth (was 64 px colour blocks).
- **Unity import:** blendshape normals are set to **None**, which removes orange flakes on the
  lips. The atlas uses bilinear filtering with mips.
- **Tuning (`FaceTunings.Tripo`):** jaw 11°, viseme gains 0.7–0.95, seal 1, smile 0.85.

### Alita

- `Companion/Characters/Add Alita Expression Channels` copies `Cheek_Raise_L/R` and
  `Eye_Squint_L/R` exactly from her CC source into five runtime meshes, so her smiles get
  cheek raise and squint. CC_Base_Body.asset grows by about 11 MB to 66.7 MB.
- Her tuning: jaw 9.5°, CC viseme gains 0.5–0.8. Her CC5 rig has no `mouthClose`, so the
  seal is skipped.

## Evidence

| File | What it shows |
|---|---|
| `speech-motion-checks.txt` | **19 PASS**: open vowels stay open, under 0.2 change per frame at 60 FPS, jaw range, settle and release, overlap, Stop, 30/120 FPS identical, diphthong order, p seal (explosive 1.00, jaw 0.15) and fast release, f/v, early rounding, tongue for l, loud vs quiet syllables (0.83 vs 0.59), three emphasis pulses, pause detection, sentence starts without snapping shut |
| `face-checks.txt` | **50 PASS** (Alita 16, Meera 17, Tara 17). Covers roles, Stop and head motion (3.7° peak, still under Reduce motion), plus the timing results below |
| `face-performance.mp4` | Frame-exact review, 1080×480 at 30 FPS, 28.5 s with audio. Three recorded Windows-speech turns (happy, concerned, curious with questions) play through the runtime code on isolated copies of Alita, Meera and Tara, side by side. Jaw peaks: 6.7° Alita, 7.8° Meera/Tara |
| `before-after.png` | Meera and Tara before and after the rig polish: neutral, aa, oo, ee, f/v and happy, from the rig-check face renders. Those renders are now posed as the app drives them: shape + jaw bone + seal |
| `live-turns.png`, `live-turns.txt` | One real local-AI voice turn per character in Play. 16 face crops per character from the app's portrait render, every 0.25 s while speaking |

Face check results in detail:

| Measure | Result |
|---|---|
| Blink rate | 14 per idle minute, 19 per minute while talking |
| Blink shape | close 33 ms, open 200 ms |
| Happy reply | smile 0.41–0.48 within a second, Duchenne cheek raise 0.33 |
| After the reply | 0.23–0.26 still at +1.5 s, 0 within ten seconds |
| Concern | inner brows 0.54 |
| Question | brows 0.18 (0.00 otherwise) |
| Thinking | look-aside 10.8° |
| Jaw and seal | jaw follows articulation × tuning; `mouthClose` seals p/b/m on Meera and Tara |

Live turn timings (this machine, warm local model):

| Character | First text | First audio | Complete |
|---|---|---|---|
| Alita | 0.11 s | 0.67 s | 6.42 s |
| Meera | 0.08 s | 0.69 s | 7.77 s |
| Tara | 0.08 s | 0.64 s | 6.50 s |

Regressions after these changes, all passing:

| Suite | Checks |
|---|---|
| Meera rig | 105 (adds "face tuning from the spec is on the roster") |
| Tara rig | 106 |
| Meera in-app | 26 |
| Tara in-app | 26 |
| Alita runtime meshes | 143 (was 123; +20 frame-count/exact-copy checks for the four new channels on five meshes) |
| Alita portrait/face | 15 |
| Body idle | 34 |
| Conversation polish | 56 |
| New chat | 14 |
| Reply replay | 11 |
| Transparent chat | 22 |
| Wardrobe UI | 8 |
| Skin-tone UI | 24 |
| Real conversation | 19 |
| Speech timing | 11 |

## Re-run

Run these in the stopped Editor with TalkingCompanion open:

- `Companion/Run Speech Mouth Motion Checks`
- `Companion/Characters/Run Face Performance Checks`
- `Companion/Characters/Check Meera Rig` and `Check Tara Rig`
- `Companion/Characters/Apply Face Tuning`: re-applies the tuning from code to the roster and
  saves the scene.
- `Companion/Characters/Render Face Performance Review`:
  - Needs recorded fixtures `artifacts/face-review/turn*.json` (ignored). Each holds an
    `emotion` and `TalkingCharacter.Reply` clips captured from the local voice service.
  - Needs ffmpeg on PATH for the video; otherwise it writes frames and `trace.csv` under
    `apps/unity/Library/FaceReview/`.

In Play, with the local service running:

- `Companion.Editor.LiveFaceCapture.Run(folder, message, 0, 1, 2)` through `Unity_RunCommand`.
  It restores the selected character afterwards.

The rig checks and review renders rewrite the PNGs in `docs/evidence/m1/<name>/`; review the
diff before committing.

## Known limits

- **Rigs:**
  - Meera's and Tara's mouth shapes are generated from fields, not sculpted.
  - At oblique angles small corner artifacts remain: a red flap at Tara's left corner on wide
    shapes, and a sliver at the end of Meera's teeth.
  - Meera's light marks on the lower lip are painted in the Tripo texture; a seam-UV pull
    softened them but did not remove them.
- **Lip-sync input and timing:**
  - Lip-sync follows the Windows SAPI viseme stream (Zira). A different TTS needs its own
    cue mapping, or the loudness envelope alone.
  - Acoustic audio/visual sync was not measured. Latency compensation subtracts one DSP buffer,
    not the measured device output latency.
- **Scale:** in the app the face is about 87 px tall in the portrait view. Fine mouth detail is
  mostly visible in close-ups and the review video.
- **Cost:** Alita's runtime body asset is a plain (non-LFS) 66.7 MB git file now, and the
  channels add about 2.6 MB of memory. No device, mobile GPU or frame-time evidence.
- **Blend files:** `models/meera` and `models/tara` `*_Rig.blend` are now rig-only. Their full
  working files are in git history (Meera, commit 1252d9a) and under `D:/Blender/` (Tara).
