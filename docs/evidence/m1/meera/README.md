# Meera — rigged second character (2026-10-08)

Owner request: rig the supplied humanoid (`3d character model.glb`, Tripo export) with cloth
physics, hair, earrings, IK and a face rig with proper blendshapes, then integrate her into the
Unity app beside Alita with the existing features. ADR-071 records the decisions.

## Source and editable files

| Item | Location |
|---|---|
| Original GLB (unchanged copy) | `models/meera/source/meera-tripo-source.glb` |
| Editable rig (live Blender 5.2.2 session, PID 13356) | `models/meera/Meera_Rig.blend` (working copy: `D:/Blender/Companion_Character_Rig_20261008/`) |
| Unity import | `apps/unity/Assets/Companion/Imported/Meera/` (FBX, rig description JSON, textures, URP materials; 18 MB). Blink frames are folded at import by `MeeraModelPostprocessor` |
| Import / validation menus | `Companion/Characters/Import Meera`, `Check Meera Rig` (stopped), `Check Meera In App` (Play) |

Source: one fused 1.02M-vertex / 1.98M-triangle surface, one 8K atlas, no skeleton or morphs.

## What was built

- **Mesh:** 81k-triangle body (face 18k, hands 7.5k kept denser), 3.3k-triangle eyeballs,
  1.4k-triangle mouth interior. Scaled to 1.60 m. Six role slots (skin, hair, kurti, palazzo,
  shoes, earrings) on one atlas plus a mouth/socket slot. Grey back-of-hair texels were tone-
  mapped to the front hair distribution with a soft front→back blend.
- **Skeleton:** 104 CC_Base-named deform bones (body, 30 finger joints, jaw, eyes) plus spring
  chains: 7 hair, 2 earring, 6 kurti panel and 2 sleeve-bell chains. Blender-only controls:
  leg/arm IK targets with knee/elbow poles, foot/hand follow, eye look target, bone collections.
- **Weights:** bone heat on the core skeleton, then harmonic jaw field, widened clavicle→arm
  blend, hair blended to the body where it rests on it, earring, kurti and sleeve chains.
- **Face:** painted eye bulges replaced by rotatable eyeballs (generated matched iris, real-time
  highlight), socket walls and hidden eyelid shells; lips cut on the measured contact line with
  inner walls, cavity, teeth and tongue. 76 body channels: 10 CC visemes, the app's blink/widen/
  smile/frown/brow names and all 52 FACE-01 ARKit-style channels (eyeLook on the eyeball mesh,
  tongue/jaw parts on the mouth mesh). Blink uses 25/50/75/100 frames.
- **Unity:** `CompanionSecondaryMotion` springs with 11 body colliders; Settings Companion
  picker (remembered per device); name-aware header, composer, typing label, greeting and
  wardrobe; wardrobe roles by material slot with per-character saved looks.

## Results

- `rig-checks.txt` — **104 PASS** (isolated hidden copy, stopped Editor): renderers/URP materials,
  all channels deform and reset, blink frames, facing, jaw direction, leg IK feet planted over
  30 s, relaxed arms, fingers, gaze, wardrobe roles/tints, springs bind/respond/settle/stay
  outside colliders, reduced motion, live GPU-skinned draw coverage.
- `app-checks.txt` — **26 PASS** (Play): picker, remembered selection, draft preserved, labels,
  springs running, framing bounds, portrait camera draws the full body, six live face channels,
  Meera wardrobe, cancelled preview not saved, greeting, switch back releases springs.
- Live local-AI turn on Meera: first text 6.5 s, first audio 7.6 s, two sentences spoken with
  happy expression; mid-speech V_Wide 34 / V_Tongue_up 16 / smile 48, jaw 2.6°
  (`live-speech-frame.png`).
- Alita regressions after the shared-code changes: 123 runtime mesh, 15 face, 34 body idle,
  56 conversation polish, 22 transparent chat, 14 New chat, 8 wardrobe UI, 24 skin-tone UI and
  11 real speech replay checks passed. `Run Portrait Layout Checks` targets the M0 mock scene and
  was not applicable here (it logged "Mock app missing").

Review images: `idle-*.png`, `gesture-*.png`, `face-*.png` (baked review renders),
`app-meera*.png` (portrait app), `blender/` (segmentation, skeleton, hair recolour, eyes,
mouth interior, blink frames, relaxed-pose weights).

## Known limits

- Blendshapes are generated deformations, not artist-sculpted; small dark corner artifacts
  can appear at strong smile/frown, and lip seams were decimated from an AI mesh.
- Tripo's back-side texture is weaker: hair back recoloured, kurti back is a faded print.
- Sleeve bells show their lighter lining when arms hang; acceptable at app scale.
- No physical-device, mobile GPU, frame-time or memory evidence; 81k triangles + 76 channels is
  similar to Alita but mobile LODs remain open.
- Rights: owner-supplied Tripo output; commercial-use rights depend on the owner's Tripo plan
  and are not independently verified (Q-010). Local prototype use only until confirmed.
