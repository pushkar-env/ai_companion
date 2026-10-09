# Tara — rigged third character (2026-10-09)

Owner request: rig the supplied humanoid (`girl_character_model.glb`, Tripo export) with cloth
physics, hair, earrings, IK and a face rig with proper blendshapes, then add her to the Unity
app with all existing features, as was done for Meera. The owner gave no name; "Tara" is a
placeholder chosen for this build and can be renamed. ADR-072 records the decisions.

## Source and editable files

| Item | Location |
|---|---|
| Original GLB (unchanged copy, SHA-256 `1fd4987f…ff976`) | `models/tara/source/tara-tripo-source.glb` |
| Editable rig (live Blender 5.2.2 session, PID 13356) | `models/tara/Tara_Rig.blend` (working copy: `D:/Blender/Companion_Character_Rig_20261008_Tara/`) |
| Unity import | `apps/unity/Assets/Companion/Imported/Tara/` (FBX, rig description JSON, 5 textures, 8 URP materials) |
| Import / validation menus | `Companion/Characters/Import Tara`, `Check Tara Rig` (stopped), `Check Tara In App` (Play) |

Source: one fused 1.02M-vertex / 1.92M-triangle surface (a single connected component), one 4K
atlas plus roughness/metal and normal maps, no skeleton or morphs. The figure is barefoot, has
long curly hair down to the waist, a crown braid, drop earrings, a leather tunic with bead
trims, side lacing and a tassel, and wide-leg jeans.

## What was built

- **Mesh:** 78.5k-triangle body (face, hands and toes kept denser), 4.3k-triangle eyeballs,
  1.7k-triangle mouth interior. Scaled to 1.60 m. Six slots on the shared atlas (skin, hair,
  tunic as Top, jeans as Bottom, earrings, trim for beads/lacing/tassel) plus a mouth/socket slot.
- **Hair/tunic separation:** the hair and the tunic back are one fused surface in the source.
  Colour cannot separate them (both are similar browns), so a ray test toward the torso axis
  counts surface layers: faces with another surface in front of them are the hair mass.
- **Texture fix:** the jeans back was painted beige; it was tone-mapped to the front denim with
  a soft front→back blend in UV space (`blender/denim-back-recolour.png`).
- **Skeleton:** 108 deform bones: 58 CC_Base-named body bones (incl. 30 finger joints, jaw,
  eyes) and 50 spring-chain bones in 5 hair chains (25 bones), 2 earring, 7 tunic-skirt panels
  (the front-left slit has no panel), a 3-bone tassel and 2 sleeve-bell chains. Blender-only controls: leg/arm IK targets
  with knee/elbow poles (pole angles from a fine search; rest deviation < 1.5e-5), foot/hand
  follow, eye look target, bone collections (Deform, Face, Dynamics, Controls).
- **Weights:** bone heat on the core skeleton, then a harmonic jaw field (limited to the face;
  see the first-run failure below), widened clavicle→arm blend, hair blended onto the body
  where it rests (forearms excluded; face-framing locks stay on the head), earring, tunic panel,
  tassel and sleeve chains; four influences per vertex.
- **Face:** painted eye bulges replaced by rotatable eyeballs with a procedural matched brown iris
  aimed straight ahead (the paint glanced sideways), socket walls and hidden eyelid shells; lips
  cut on the measured contact line with inner walls, cavity, teeth and tongue. 76 body channels:
  10 CC visemes, the app's blink/widen/smile/frown/brow names and all 52 FACE-01 ARKit channels
  (eyeLook on the eyeball mesh, jaw/tongue parts on the mouth mesh). Blink has 25/50/75/100 frames.
- **Unity:** `CompanionSecondaryMotion` springs (17 chains, 50 joints) with 12 body colliders;
  Tara appears in the Settings Companion picker after Alita and Meera; name-aware header,
  composer, greeting and wardrobe; per-character saved look `Companion.Appearance.v1.Tara`.
  Barefoot, so the wardrobe offers no shoe colour; trims keep their colour under tints.

## Results

- `rig-checks.txt`: **105 PASS** (isolated hidden copy, stopped Editor): renderers/URP materials,
  all channels deform and reset, blink frames, facing, jaw opens the chin 21.5 mm, feet planted
  over 30 s (0.00 mm slip), relaxed arms, 30 finger joints, gaze, wardrobe roles/tints/trim,
  springs bind (50 joints, 12 colliders), respond (peak 11.3 cm), settle (0.000 mm/frame), stay
  outside colliders (-0.2 mm, within the 4 mm tolerance), reduced motion, live GPU-skinned draw
  (106k covered pixels).
- `app-checks.txt`: **26 PASS** (Play): roster Alita, Meera, Tara; picker, remembered selection,
  draft kept, labels, springs running, 1.73 m framing bounds, portrait camera draws 273k opaque
  pixels, six live face channels drive and reset, wardrobe without shoe colour, cancelled preview
  not saved, greeting, switching back to Alita releases the springs.
- Live local-AI turns on Tara (qwen3:8b + Windows speech, three turns): every reply streamed,
  spoke and finished; expression `happy`. App timings measured from the start of the turn request:
  first text 0.10 s, first audio 0.75–0.84 s, reply completed 3.8–6.9 s (see Known limits).
  Mid-speech portrait frame: jaw 2.55°, V_Tight_O 33, V_Affricate 14, smile 48/48
  (`live-speech-portrait.png`); full app view mid-speech in `live-speech-frame.png`.
- **First run failed, then fixed:** the first `Check Tara Rig` run stopped at `V_Open` (285 mm).
  The jaw field leaked onto 446 front tunic/lacing vertices below the neck (the flood fill
  followed the lacing line). The jaw field is now limited to face skin above 1.30 m; no channel
  moves anything below the neck, and the re-export passed all 105 checks.
- Regressions after the shared-code changes (Meera and Alita): Meera rig 104 and in-app 26 (now
  listing three characters), Alita runtime meshes 123, portrait/face 15, body idle 34,
  conversation polish 56, transparent chat 22, New chat 14, wardrobe UI 8, skin-tone UI 24, real
  speech replay 11. All passed. Editor ended stopped, TalkingCompanion saved and clean (7 roots),
  Alita still the default, Console free of errors and warnings.

Review images: `idle-*.png`, `gesture-*.png`, `face-*.png` (baked review renders), `app-tara*.png`
(portrait app), `live-speech-*.png`, `blender/` (source and budget, segmentation, hair/tunic
separation, denim recolour, skeleton, eyes, mouth interior, blink frames, relaxed-pose weights,
viseme and expression sheets).

## Face polish update (2026-10-09, ADR-073)

Tara's mouth was re-rigged with the skill's stage 12:

- **Jaw skin weights:** corner-aware.
- **Mouth shapes:** visemes and ARKit mouth shapes are now lip-only; the app's jaw bone does the
  opening, and `mouthClose` seals over the gap.
- **Mouth interior:** tongue and lower incisors re-placed, teeth arches rebuilt.
- **Mouth atlas:** 256 px with painted teeth.
- **Unity import:** blendshape normals are now None.

`rig-checks.txt` now has **106 PASS**. It was 105; the new check is "face tuning from the spec
is on the roster". The `face-*.png` renders are posed as the app drives them: shape + jaw bone +
seal. `face-speech-pbm.png` is new. The in-app checks still pass at 26.

Talking-face results, review video and before/after renders:
`docs/evidence/m1/face-performance/README.md`.

## Known limits

- Blendshapes are generated deformations, not artist-sculpted, and were tuned on renders only.
- Hair is one decimated shell with spring bones, not strand simulation; the curls swing as
  locks. The tunic back and the hair also share vertices where the source fused them.
- The denim back is a recolour of a beige paint, so its seams are softer than the front.
- The left eye's painted lash shadow is darker than the right; both lid shells reuse the right
  eye's skin tone so the closed lids match.
- Timing caveat: the local service streams the text quickly; first text 0.10 s and first audio
  under 1 s here are this machine with a warm model, not device or network latency.
- No physical-device, mobile GPU, frame-time or memory evidence. Each character adds one hidden
  but loaded rig to the scene (now three); mobile LOD and memory budgets remain open.
- Rights: owner-supplied Tripo output; commercial-use rights depend on the owner's Tripo plan
  and are not independently verified (Q-010). Local prototype use only until confirmed.
