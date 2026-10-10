---
name: tripo-character-rig
description: Turn an owner-supplied Tripo (or similar AI-generated) humanoid GLB into a fully rigged, talking companion in the Unity app. Covers the live Blender MCP pipeline (segmentation, CC_Base skeleton, eyes, mouth, weights, 76 blendshapes incl. 52 ARKit, face polish with corner-aware jaw weights and lip-only visemes, spring chains, IK) and Unity integration as a selectable character beside Alita/Meera/Tara/Arjun with per-character face tuning and a per-character local voice (female or male). Stage 13 makes a character modular (base body + swappable garments, male/female category-gated outfits, garment spring chains, procedural garment textures) and adds new outfits from a reference image. Use when the user hands over a new character model (.glb/.fbx/.obj) to "rig and add to the app", or asks for outfits/wardrobe customisation on a rigged character.
---

# Tripo GLB → rigged in-app companion

This skill comes from the Meera build (2026-10-08, ADR-071), extended by Tara (ADR-072), the
face polish of both (2026-10-09, ADR-073), Arjun, the first male companion (ADR-074), and Arjun's
modular wardrobe (2026-10-10, ADR-075, stage 13). Meera took one long session; following these
stages in order avoids the dead ends hit then. The Blender scripts
under `blender/` are the **verbatim working code** from that build, kept in pipeline order.
Treat them as tested templates. Every coordinate, threshold and object name in them was
measured on Meera (1.60 m, facing −Y, Blender Z-up), so re-measure before reusing a block.

## 0. Before you start

1. Read `AGENTS.md`, `docs/MEMORY.md`, ADR-071 in `docs/DECISIONS.md` and
   `docs/evidence/m1/meera/README.md`.
2. Pick the character name `<Name>`. If the user gave none, choose a placeholder and say so
   in the final report. Every asset, material and object uses it as a prefix
   (`<Name>_Body`, `<Name>_Skin`, …). Pick the voice key (`female` → Zira, `male` → David).
   Render the source first: the product is adults-only, so check that the body reads as an
   adult (about 7 heads tall) and scale to an adult height. Ask the owner if it reads as a minor.
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

| 13 | **Modular wardrobe** (ADR-075; Arjun 2026-10-10). Split the fused mesh into a base body (skin, hair, face rig) and garments; rebuild what the split exposes (trouser waist, open shirt, tee); add new garments from the owner's reference; weight, give each shirt its own spring chains, texture and export one FBX per garment | `13_modular_garments.py` + `textures/*.py` | Every part rendered alone; the signature look renders identical to before; pose tests (arms down, raised, twisted) show no inner layer through an outer one |

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

### Stage 13: modular wardrobe (base body + garments)
Use it when the owner wants swappable outfits. The owner's rule: male characters never get female
outfits, and the other way round. Arjun was first (ADR-075). Do the work in a fresh `.blend`,
appending the rig file into it.
1. **Split** (13a): store vertex normals, copy the body once per garment, delete the other
   regions, restore the stored normals as custom normals. The base body keeps every shape key.
   Garments get none; check that no shape moves garment vertices first.
2. **Render every part alone** (13b). Segmentation leftovers only show once parts separate. Move
   the strays: on Arjun, a collar strip and shoulder flecks went to the shirt, and nails to the base.
3. **Give garments clean seams** (13c): the visible strip below a shirt hem belongs to the
   trousers. Re-cut at a plane below tangled geometry and loft a real waist and waistband so open
   tops show a proper waistband. Restore moved faces' texture coordinates face by face.
4. **New tops from old ones** (13d): to open a shirt, remove the folded button strip instead of
   cutting through it. Rotate the panels along a smoothed shell grid so every vertex moves.
   Straighten the hem, fold the open edges with radial normals, then clean flaps, remnants and
   needles.
5. **Inner layers** (13e): loft them as rings. Clamp along rays from the body axis (skin + 3.5 mm,
   outer layer − 3 mm). Inset or delete parts that are always hidden.
6. **Weights** (13g): re-weight moved panels from the source garment at their new positions, the
   same source as the inner layer. Chain bones belong to the garment (prefix them per garment).
   Strip weights to bones the garment does not export.
7. **Textures** (13h, outside Blender, `python -I`):
   - `textures/uvpack.py` keeps the original charts, splits bridging "orphan" faces and
     shelf-packs at a uniform texel density.
   - `paint_*.py` rasterize positions, normals and old texture coordinates per texel, then paint
     procedural fabric. Fold shading comes from the original texture's luminance; stitching,
     plackets and buttonholes are painted analytically in 3D.
8. **Export** (13i): one FBX per garment, with deform flags choosing CC_Base plus that garment's
   chain bones and share bones. Add `<File>.item.json` for chains and `shares`. Triangulate n-gons.
9. **Owner review fixes** (13j, Arjun 2026-10-10), all worth doing up front on the next character:
   - **Neck skin back to the body.** The 13b colour rule also moved shadowed neck skin into the
     shirts. The original texture hid it; a recoloured copy painted the neck blue. Select it by
     colour plus contact with the body's open boundary, take the faces from the pre-split
     checkpoint (weights, face-shape deltas, normals), join and weld into the body, delete them
     from every garment.
   - **Armpits.** A T-pose shirt has a 5–10 cm underarm fold, and inherited weights gave the side
     panel upper-arm weight 10–15 cm below the armpit, so raised arms dragged the side into a web.
     Add a half-rotation share bone per shoulder (`Share_<S>_Upperarm`: clavicle child, same rest
     as the upper arm; the app turns it) and re-weight the underarm in arm-aligned coordinates
     (`armpit_weights`: chest → share → arm across the fold, 40 smoothing passes). Unsmoothed
     weights crease the front fold and pinch the hanging arm.
   - **Inner layer vs restored skin:** lift the tee where neck skin points come through it
     (`tee_neckline`).
   - **Edges on light fabric:** a saw-tooth collar top shows the hair's matching teeth. Raise the
     notches to lines between the tooth tips, never lower (`collar_tips`). Drop ear triangles at
     torn ends (`lapel_end`, `collar_flaps`).

## 2. Unity integration

Details: `reference/unity-integration.md`. In short:
1. Copy `export/` into `apps/unity/Assets/Companion/Imported/<Name>/` (FBX, rig.json,
   `Textures/`). Keep `.meta` GUIDs on re-imports.
2. Add `Editor/<Name>CharacterSetup.cs` with a `CharacterSpec` and the three menu items
   (copy `TaraCharacterSetup.cs`). The shared import and checks are already generic (ADR-072).
   Set `Face=FaceTunings.Tripo()` for a stage-12 rig and `Voice="male"` for a male companion.
   The import copies both onto the roster entry, and `Companion/Characters/Apply Face Tuning`
   re-syncs the tuning from code. Add the new spec to `FaceTuningSetup.For`.
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
   mid-speech frame (`LiveFaceCapture.Run` takes 16 face crops while the character speaks).
   If you changed `services/voice-agent` code, restart the service first: the running Node
   process keeps the old code. Run `node tests/e2e/check-talking-service.mjs`.
7. Modular wardrobe (stage 13): in the spec, fill `Category`, `Garments` (id, display name, file,
   object, slot, materials in submesh order, hides) and `Outfits`; the first outfit is the
   signature look. Add `Slots` with `Normal=` for the garment atlases. Copy `Wardrobe/*.fbx` and
   `*.item.json` and rerun Import.
   - The rig check adds about 25 wardrobe checks, including the category rule both ways, share
     joints at half the upper arm, skin in front of the neck (rays toward the neck axis must hit
     the body before any garment) and the side panel staying with the chest as the arms rise.
     It renders `<outfit>-neck` and `<outfit>-armpit-{level,raised,stretch}` for every outfit.
   - The in-app check adds an outfit round trip through Style. It starts from the default look
     whatever the device saved, and restores the owner's look afterwards.
   - See `reference/unity-integration.md`.

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
| Ragged shoe/trouser boundary (light trouser hem over white sneakers, Arjun) | Colour thresholds | Watershed (min-max path cost over colour differences) on the face graph, seeded at the soles and the shins |
| Fringe locks and painted brows/eyes are both dark | Colour alone | Ray test into the head: another surface within about 4.5 cm behind means a hair lock, otherwise paint. Dark faces above the brow line are fused fringe |
| Jaw field floods down the shirt front | The lip flood (front, small x) reaches clothing | Restrict the lip and chin sets to face skin above the chin. Fixed-0 regions win over fixed-1 |
| Neck skin crumples at the collar when the head turns | Gaze turns only the head bone; auto weights put head weight low on the neck | Harmonic neck gradient from the collar line (0) to the jaw/skull line (1). Skin under the collar is pinned; split head/jaw by the jaw field |
| Slits in the collar once the arms lower | Upper-arm weights on the collar | Move upper-arm weight to the clavicle within about 13 cm of the neck axis |
| Dark streak inside the open mouth | Cavity clamp rays escaped through the lip slit; the cavity front poked out | Clamp cavity vertices to the seam depth + 5.5 mm |
| Creases from the mouth corners on oo/pucker (wide mouth, decimated cheeks) | The vermilion field steps under large narrowing | Skin-only wider envelope for width/protrude, then about 10 iterations of displacement smoothing with seam and walls pinned |
| IK pole "search" never reaches zero deviation | Candidate angles derived from the constraint value being mutated during the test | Build the candidate list first; sweep 10°, then refine |
| A male character speaks with the female voice | The voice used to be global | `Voice="male"` in the spec; the service maps voice keys to installed voices |
| Rig check throws "Sequence contains no matching element" | Old checks assumed earrings and long hair (40+ spring joints) | Fixed in ADR-074; keep checks generic for every new rig |
| After the split a collar strip or flecks stay on the base body, nails go with the shirt (stage 13) | Segmentation errors were invisible while everything was one textured surface | Render every part alone; reassign by texture colour and position; move faces between objects with join + seam weld |
| Garment hem is a ragged geometric edge (stage 13) | The colour boundary between shirt and trousers was jagged | Move the band below the hem to the trousers; straighten the new hem to its lower envelope (`hem_edges4`) |
| Dangling strips at an opened shirt's edge | Cut through the folded, multi-layer placket | Delete the whole button strip, then open; add new buttons and paint buttonholes |
| Spikes and stretched buttons after opening a shirt | Per-vertex ray casts failed through holes in the shell | Open along a smoothed shell grid (median + blur) so every vertex moves coherently |
| Inner tee pokes through the panels when the arms rise | Opened panels kept weights from their old positions | Re-weight moved panels from the source shirt at their new positions (same source as the tee) |
| Spiky fold triangles on garment edges | Vertex normals on degenerate or flattened faces | Fold with radial normals; dissolve degenerate faces; smooth the inward tangent along the chain |
| Unity: "N vertices with no weight… assigned to bone #0" | Garment vertices weighted only to bones that file does not export (hair) | Drop those weights and refill from neighbours before export |
| Unity: "polygon … self-intersecting … discarded" | N-gons from `holes_fill` | Triangulate n-gons before export |
| Import throws MissingComponentException on a new garment | In the Editor `GetComponent` returns a fake null for missing built-in components, and `??` keeps it | `TryGetComponent` (`CharacterSetup.Ensure<T>`) |
| Blender memory jumps, materials named `.001` | `bpy.data.libraries.load` duplicated images and materials | Remap duplicates to the originals and purge orphans (`dedupe`) |
| Comparing evidence PNGs with HEAD fails ("cannot identify image") | Evidence PNGs are LFS pointers | `git show HEAD:<png> \| git lfs smudge > tmp.png` |
| Blue (garment-coloured) patch on the neck in a new outfit, invisible in the signature look (stage 13) | The 13b rule "dark below z 1.52 → shirt" took shadowed neck skin; the original texture still showed skin there | 13j neck fix from the pre-split mesh; the rig check casts rays at the neck for every outfit |
| Raised arms pull the shirt's side into a web from elbow to hem; a horizontal arm looks like a bat wing (stage 13) | T-pose underarm fold plus upper-arm weights far down the side panel | Share bones + `armpit_weights`; the rig check measures the side band against chest-rigid motion |
| New underarm weights crease the front fold and pinch the hanging arm | Analytic weights with sharp gradients | Laplacian-smooth the weight field (40 passes) inside a dilated region mask |
| Restored neck skin shows as slivers beside the lapel | The tee had been fitted to the old body | `tee_neckline`: lift tee triangles hit by skin points; bound the skin samples (T-posed forearms sit at neck height) |
| Saw-tooth collar edge shows on light fabric | Colour boundary cut along triangles; the hair's bottom edge interlocks with it | `collar_tips`: raise notches to lines between tooth tips, at most 18 mm, never lower |
| In-app check fails at the outfit step after the owner saved another outfit | The check assumed the default look | `InApp` clears the look pref first and `Finish` restores it |
| Live-job edits lost on reload | `bpy.data.is_dirty` stays False after data-API edits made in jobs | Save explicitly (`wm.save_mainfile`) after each verified stage |

## 5. Definition of done
- Every rig check and in-app check passes (the in-app check includes the companion's voice), and
  so do the face performance, speech motion and talking-service checks. Alita regressions still pass. Editor is back to stopped with the scene clean. Alita
  stays the scene default. The owner's character pref (`Companion.Character.v1`) is left as
  found.
- Live speech turn on the new character recorded.
- Evidence and docs written. Limits stated honestly: generated (not sculpted) shapes, weak
  back texture, no device/perf evidence, rights unconfirmed.
- Modular wardrobe (stage 13): wardrobe rig checks and the in-app outfit round trip pass. The
  signature look is unchanged against the committed renders. Other characters' rig, in-app and
  wardrobe suites still pass. Pose tests show no inner layer through an outer one (report any
  extreme-pose exception). Close-ups of the neck and of every armpit pose (level, raised,
  stretch) look natural in every outfit.
- Nothing committed unless the user asked.
