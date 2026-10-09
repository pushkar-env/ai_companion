---
name: tripo-character-rig
description: Turn an owner-supplied Tripo (or similar AI-generated) humanoid GLB into a fully rigged, talking companion in the Unity app. Covers the live Blender MCP pipeline (segmentation, CC_Base skeleton, eyes, mouth, weights, 76 blendshapes incl. 52 ARKit, face polish with corner-aware jaw weights and lip-only visemes, spring chains, IK) and Unity integration as a selectable character beside Alita/Meera/Tara with per-character face tuning. Use when the user hands over a new character model (.glb/.fbx/.obj) to "rig and add to the app".
---

# Tripo GLB → rigged in-app companion

This skill comes from the Meera build (2026-10-08, ADR-071), extended by Tara (ADR-072) and
the face polish of both (2026-10-09, ADR-073). Meera took one long session; following these
stages in order avoids the dead ends hit then. The Blender scripts
under `blender/` are the **verbatim working code** from that build, kept in pipeline order.
Treat them as tested templates. Every coordinate, threshold and object name in them was
measured on Meera (1.60 m, facing −Y, Blender Z-up), so re-measure before reusing a block.

## 0. Before you start

1. Read `AGENTS.md`, `docs/MEMORY.md`, ADR-071 in `docs/DECISIONS.md` and
   `docs/evidence/m1/meera/README.md`.
2. Pick the character name `<Name>`. If the user gave none, choose a placeholder and say so
   in the final report. Every asset, material and object uses it as a prefix
   (`<Name>_Body`, `<Name>_Skin`, …).
3. Hard rules (from AGENTS.md):
   - Blender: only the **live** MCP session. Probe PID/file/scene first. Create a **fresh
     unique .blend in the same process** (`live.new_file`) under
     `D:/Blender/Companion_Character_Rig_<YYYYMMDD>/`. Run heavy work as staged
     `live.submit` jobs and poll `live.status()`. Never use background/CLI Blender or
     computer-use, and never edit the scene while a job runs.
   - Unity: keep the initial Play state and the user's Editor layout and Game-view
     selection. Never run the portrait-preview helpers. Keep existing GUIDs. The app stays
     portrait-only.
   - Never print `artifacts/talking-character/session.json`. Avoid raw `Editor.log`.
   - Commit or push only when asked. `.blend`/`.glb`/`.fbx`/`.png`/`.jpg` are LFS-tracked.
4. Copy the GLB unchanged to `models/<name>/source/<name>-tripo-source.glb`. Log the
   commercial-rights question for the Tripo output in `docs/requirements/QUESTIONS.md`
   (Meera: Q-010).

## 1. Blender pipeline (live session)

Run `blender/01_session_helpers.py` blocks first. They install `companion_submit`,
`companion_shoot` (Workbench review renders into `<workdir>/review/`) and `set_shapes` in
`bpy.app.driver_namespace`. **Look at a review render after every stage**; most Meera bugs
were caught only by images.

| # | Stage | Script | Output / check |
|---|---|---|---|
| 1 | Import GLB, working copy, scale to adult height, merge UV-split verts | `02_import_and_budget.py` | `<Name>_Body`; height measured, scale = target/height |
| 2 | Per-region decimation: Meera 2M → 81k tris (face 0.22, hands 0.08, hair/ears 0.06, body 0.03, shoes 0.015). Clear custom normals, smooth shade | `02` | ~70–90k tris; face and fingers still read in close-ups |
| 3 | Segment by texture colour + position, using flood-grow and majority smoothing, into role slots `<Name>_Skin/_Hair/_Top/_Bottom/_Shoes/_Earrings` (Meera: Kurti/Palazzo). Store a `companion_region` face int attribute: 0 skin, 1 hair, 2 top, 3 bottom, 4 shoes, 5 jewellery; later 6 socket wall, 7 lip wall, 8 eyelid shell | `03_segmentation.py` | Flat-colour front/back/side renders show clean regions |
| 4 | Texture fixes: Tripo back sides are often grey or washed out. Quantile-remap back-hair texels to front tones with a soft front→back blend in UV space | `04_hair_texture_fix.py` | Back and side head renders match the front |
| 5 | Skeleton: **CC_Base names** (104 deform bones incl. 30 finger joints, JawRoot, L/R_Eye), measured from cross-sections and finger components. Spring chains: `Hair_<chain>_NN`, `Earring_<S>_NN`, skirt/hem panels, sleeves | `05_skeleton.py` | Bone-stick renders front, side, hand and head |
| 6 | Eyes: RANSAC-fit the painted eye bulges, delete them, add socket walls (region 6), rotatable UV-sphere eyeballs with polar UVs, procedural matched iris texture `<Name>_Eyes.png` (1024×512) | `06_eyes.py` | Both irises identical; no beige sclera; rims smooth |
| 7 | Mouth: lip contact curve from manual anchors, then a Dijkstra cut in a narrow band. Split the seam, add inner lip walls (region 7), a cavity ellipsoid clamped inside the head, teeth arches and tongue as `<Name>_Mouth` (64×64 colour-block texture). Harmonic jaw field `jaw_w` with lip sets | `07_mouth_and_jaw.py` | 14° test jaw-open: chin moves, upper lip and nose stay |
| 8 | Weights: bone heat on the core skeleton only (dynamic and face bones excluded), then overrides: wide clavicle→upper-arm blend + smoothing, smooth face/neck blend + jaw split, earrings, hair chains with body attach (lower-arm bones excluded), skirt panels, sleeves. Limit to 4 influences. Bind eyes/mouth | `08_weights.py` | Relaxed-arm pose renders: no shoulder fins, chin tears or hair stuck to forearms |
| 9 | Face rig: socket/lip-wall colour blocks, eyelid shells (region 8, φ ±0.30 past the corners), face fields, then `code_shapes` → 76 body channels + mouth and eyeball parts | `09_face_rig_and_blendshapes.py` | `set_shapes` renders of blink 25/50/75/100, visemes, smile, pucker, press |
| 10 | Blender-only controls: IK_Foot/Knee/Hand/Elbow, Look_Target, bone collections. **Run the pole-angle 90° search**; the analytic angle flipped Meera's chains 180° | `10_ik_controls.py` | Rest-pose deviation ≈ 0 for every bone |
| 11 | Export: textures saved raw, FBX, `<Name>.rig.json` (chains + colliders, colliders shrunk off rest joints, colliders < 25 mm dropped) | `11_export.py` | `export/` holds FBX + 5 textures + rig.json |
| 12 | **Face polish** (run after 9/10, then export with 11). Corner-aware harmonic jaw weights (lip halves meet at 0.5 at the corners). Rebuild visemes and ARKit mouth shapes as lip-only postures with no jaw inside. Tongue and lower incisors placed from a midline section. Teeth arches rebuilt with `rebuild_teeth`. 256 px mouth atlas with painted teeth | `12_face_polish.py` | Culled Workbench close-ups posed like the app (shape + jaw/22 + seal): aa oval, oo round, p/b/m sealed, f/v lip under the teeth, no cheek cracks |

### Blendshape contract (names the app and checks rely on)
- CC visemes: `V_Open V_Explosive V_Dental_Lip V_Tight_O V_Tight V_Wide V_Affricate V_Tongue_up V_Tongue_Out V_Tongue_Raise`
- App expressions: `Eye_Blink_L/R` (+ in-between keys `Eye_Blink_<S>__f25/__f50/__f75`, folded
  into frames at Unity import), `Eye_Widen_L/R`, `Mouth_Corner_Pull_L/R`,
  `Mouth_Corner_Depress_L/R`, `Brow_Raise_In_L/R`, `Brow_Raise_Outer_L/R`, `Brow_Drop_L/R`
- All 52 ARKit names (`browDownLeft` … `tongueOut`). `eyeLook*` go on the eyeball mesh;
  jaw and tongue go on the mouth mesh too, with the same names.
- Since stage 12 the visemes and ARKit mouth shapes are **lip-only**. The app's JawRoot bone
  does all the opening, at `FaceTuning.jawDegrees` (Tripo 11°, Alita 9.5°). `jawOpen` and
  `mouthClose` keep a 22° jaw reference. The app drives `mouthClose` at
  closure × jaw / 22 to seal the lips over the bone's gap on p/b/m and rounded vowels.
- The face layer (`CompanionFace`) uses the first name it finds for each role:
  - `Mouth_Corner_Pull_*` or `mouthSmile*`
  - `Cheek_Raise_*` or `cheekSquint*`
  - `Eye_Squint_*` or `eyeSquint*`
  - `Brow_Raise_In_*`
  - `Brow_Raise_Outer_*` or `browOuterUp*`
  - `Brow_Down_*`, `Brow_Drop_*` or `browDown*`
  - `Mouth_Press_*` or `mouthPress*`
  - `mouthClose`
  - Missing roles are skipped. A squint falls back to a light blink.
- Jaw convention: JawRoot roll must put bone local Z along world −X (same as Alita), so the
  app's `jawRest * Euler(0,0,-deg)` opens the mouth. The build script does this with
  `align_roll((1,0,0))`.

### FBX export settings (do not change)
Selected rig + Body + Eyes + Mouth, armature in **Rest Position**, all shape keys 0,
`object_types={ARMATURE,MESH}`, `use_mesh_modifiers=False`, `add_leaf_bones=False`,
`use_armature_deform_only=True`, `apply_scale_options='FBX_SCALE_ALL'`, `axis_forward='-Z'`,
`axis_up='Y'`, `bake_anim=False`, `path_mode='STRIP'`. Unity position = (−bx, bz, −by), and
the character faces +Z.

## 2. Unity integration

Details: `reference/unity-integration.md`. In short:
1. Copy `export/` into `apps/unity/Assets/Companion/Imported/<Name>/` (FBX, rig.json,
   `Textures/`). Keep `.meta` GUIDs on re-imports.
2. Add `Editor/<Name>CharacterSetup.cs` with a `CharacterSpec` and the three menu items
   (copy `TaraCharacterSetup.cs`). The shared import and checks are already generic (ADR-072).
   Set `Face=FaceTunings.Tripo()` for a stage-12 rig. The import copies it onto the roster
   entry, and `Companion/Characters/Apply Face Tuning` re-syncs every entry from code.
3. In the stopped Editor with TalkingCompanion open, run
   `Companion/Characters/Import <Name>`. It creates URP materials, configures the model,
   builds springs from rig.json, adds the prefab instance (inactive) at Alita's transform,
   registers the `CharacterOption` and saves the scene.
4. Run the rig checks (stopped) and the in-app checks (Play), then restore the stopped state.
   Re-run the Alita regression suites listed in the reference.
5. Run `Companion/Characters/Run Face Performance Checks` (16–17 per character) and
   `Companion/Run Speech Mouth Motion Checks`. Optionally run `Render Face Performance Review`,
   a frame-exact talking video of every roster character; it needs ffmpeg.
6. Do one live local-AI turn on the new character. Record first-text/first-audio times and a
   mid-speech frame (`LiveFaceCapture.Run` takes 16 face crops while she speaks).

## 3. Evidence and docs (part of done)
- `docs/evidence/m1/<name>/`: README, rig-checks.txt, app-checks.txt, idle/face/gesture/app
  PNGs, `blender/` process images.
- `models/<name>/`: `<Name>_Rig.blend` copy, source GLB, README with re-export steps.
- New ADR in `DECISIONS.md` (or extend ADR-071 if nothing new was decided). Update
  `STATUS.md`, `MEMORY.md`, `QUESTIONS.md` (rights) and `25_IMPLEMENTATION_PLAN.md`.

## 4. Pitfalls already paid for

| Symptom | Cause | Fix |
|---|---|---|
| Exported textures darker than source | `image.save_render` applies the AgX view transform | `img.save(filepath=..., quality=92)` (raw) |
| "BMesh data … removed" during the eyeball build | Created a bmesh layer mid-loop | Create layers before iterating, or not at all |
| Mismatched or beige irises, lid streaks | Sampling the painted eye texture | Procedural matched iris (call 63) |
| Lip path cuts the wrong corner | Nearest-vertex search dominated by y | Manual curve anchors + Dijkstra in a ±3 mm band |
| Jaw opens the hem or chest | Lower-lip flood reached the body front | Zero `wj` below the neck (Meera z<1.30) and restrict lip sets |
| Chin tear | Hard face-only weight boundary | Smoothstep blend from neck weights to head-only |
| Shoulder fins | Hair attached to the clavicle stretched the top | Wider clavicle→arm blend + smoothing; hair may follow upperarm/clavicle, not forearm |
| Blink smears painted lid / cuts through the eye | Linear chords and painted lids | Eyelid shell geometry + 25/50/75 in-between frames |
| IK chains flip 180° | Analytic pole angle | 90° search minimizing rest deviation |
| Pucker moves nostrils, cracks corners | Separate lip/skin fields | One smooth radial field, z-gated below the nose |
| Body invisible in the app ("mesh data size … vertex stride") | `CopySerialized` runtime mesh copy (and a 642 MB YAML asset) | Fold blink frames in an `AssetPostprocessor` at import; use the FBX mesh directly |
| Front review render blank | First render after setup | Warm-up render before capture |
| Wardrobe ignores the new character | Material slots not named with role suffixes | Name slots `<Name>_Skin/_Top/_Bottom/_Hair/_Shoes`; anything else stays untinted (see reference) |
| Hair and garment are one fused surface, similar colour (Tara) | Colour thresholds can't split them | Count surface layers along a ray from each face toward the torso axis: faces with another surface in front are the outer layer (hair) |
| `V_Open` moves 285 mm at 60% (Tara) | Lower-lip flood followed front lacing down the torso | Zero `jaw_w` and the lip sets outside face skin above the neck before generating shapes; the rig check catches it |
| Washed or wrongly coloured back of a garment (Tara: beige jeans back) | Tripo back-side paint | Same quantile tone-map as the hair fix, restricted to that slot's texels |
| Face index map or saved labels misalign after surgery | Eye/lip cuts reorder faces | Recompute per-face data from the current mesh; never index old per-face arrays after a bmesh edit |
| One closed lid darker than the other | Lid shell UV sampled painted lash shadow | Use one skin texel for both shells, picked away from painted lashes |
| Every viseme opens as the same wide box or slit | Stage-09 shapes bake a jaw rotation in, and jaw weights jump 0.2→0.8 one vertex from each corner | Stage 12: corner-aware harmonic jaw weights and lip-only shapes; the app's bone opens the jaw |
| Cheek cracks or a dark line under the lower lip in mouth shapes | Hard depth cutoff, vermilion weights reaching past the corners, free lip centres | Smooth zone fields with a depth envelope and lateral falloff; pin the lip centres rigid to their jaw side |
| Orange "flakes" on the lips in Unity only | Unity's calculated blendshape normals flip on the thin lip walls | `importBlendShapeNormals = None` (CharacterSetup does this) |
| Teeth hidden in Blender reviews but visible in Unity | Inward-facing cavity front wall draws over them in Workbench | Turn backface culling on for every mouth review |
| Tongue shows through the chin, or sits in front of the incisors when the jaw opens | Stage-07 tongue too long and low; lower incisors too low | `adjust_interior`, checked on a midline (abs(x) < 4 mm) section plot of body, teeth, tongue and cavity |
| Outer teeth poke through the cheek | Uniform arch widening | `rebuild_teeth`: a wider, deeper parabola that keeps the ring heights |
| Lip classification misses part of the wall, or duplicate seam verts move apart | Position thresholds across the cut seam | Flood-fill the lip classes over the seam from the cut chains |
| Magenta mouth texture | Image path changed before the file existed | Set `filepath_raw` only when saving (STEP_TEETH) |
| Blocky teeth or mouth colours in close-ups | 64 px point-filtered atlas | 256 px atlas, bilinear with mips, uncompressed (CharacterSetup) |

## 5. Definition of done
- Every rig check and in-app check passes, and so do the face performance and speech motion
  checks. Alita regressions still pass. Editor is back to stopped with the scene clean. Alita
  stays the scene default. The owner's character pref (`Companion.Character.v1`) is left as
  found.
- Live speech turn on the new character recorded.
- Evidence and docs written. Limits stated honestly: generated (not sculpted) shapes, weak
  back texture, no device/perf evidence, rights unconfirmed.
- Nothing committed unless the user asked.
