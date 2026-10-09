# Arjun — first male companion (2026-10-09)

The owner asked to add a supplied male character (`3d boy model.glb`, Tripo export) to the app
in the same way as Meera and Tara. Requirements:

- rig him with cloth physics, blendshapes, lip-sync and facial animation;
- make sure he "comes alive" and looks polished and realistic in the app.

The owner gave no name. "Arjun" is a placeholder chosen for this build and can be renamed. He
is presented as an adult:

- the body has adult proportions (about 7 heads tall);
- he is scaled to 1.78 m including hair;
- his stylised, youthful face matches Meera and Tara.

ADR-074 records the decisions.

## Source and editable files

| Item | Location |
|---|---|
| Original GLB (unchanged copy, SHA-256 `5dd7ab9c…834d57`) | `models/arjun/source/arjun-tripo-source.glb` |
| Editable rig (live Blender 5.2.2 session, PID 16028) | `models/arjun/Arjun_Rig.blend` (rig only, 20.7 MB). Working file: `D:/Blender/Companion_Character_Rig_20261009_Arjun/` |
| Unity import | `apps/unity/Assets/Companion/Imported/Arjun/` (FBX, rig description JSON, 5 textures, 8 URP materials) |
| Import and validation menus | `Companion/Characters/Import Arjun`, `Check Arjun Rig` (stopped), `Check Arjun In App` (Play) |

The source is one fused surface:

- 990k vertices and 1.92M triangles, a single connected component;
- one 4K atlas plus roughness/metal and normal maps;
- no skeleton or morphs.

He wears a dark-brown untucked button-up shirt with rolled sleeves and a breast pocket, light
grey straight trousers and white sneakers. He has short, spiky black hair with a fringe over the
forehead.

## What was built

- **Mesh:**
  - Body: 84k triangles (face, hands and hair tufts kept denser).
  - Eyeballs: 4.3k triangles. Mouth interior: 1.7k triangles.
  - Material slots: skin, hair, shirt (Top), trousers (Bottom), shoes and a Trim slot for the
    shirt buttons, so wardrobe tints keep them white. A mouth/socket slot is added for the
    socket and lip walls.
- **Segmentation:** colour thresholds alone could not separate everything, so three extra
  tests were needed:
  - **Shoes and trousers:** the trouser hem lies over the near-white sneakers. A watershed on
    the face graph, seeded at the soles and the shins, follows the hem's colour edge.
  - **Fringe and painted brows:** both are dark, so a ray test into the head decides. A surface
    within 4.5 cm behind a dark face means a hair lock; otherwise it is paint.
  - **Back texture:** no fix was needed.
- **Skeleton:** 92 deform bones.
  - 58 CC_Base-named body bones, including 30 finger joints, jaw and eyes.
  - 34 spring-chain bones: 10 short-hair chains (fringe, sides, back and crown tufts) and
    8 shirt-hem panels around the hips.
  - Blender-only controls: leg/arm IK with knee/elbow poles (rest deviation below 7e-6), foot
    and hand follow, an eye look target, and bone collections.
- **Weights:**
  - Bone heat on the core skeleton, then a harmonic jaw field limited to face skin. Its first
    flood leaked onto the shirt front and was fixed.
  - Widened clavicle→arm blend.
  - **Collar:** sits on the neck base and chest. Head and upper-arm weights move to the neck
    and clavicles, so lowered arms and head turns no longer crumple it.
  - **Neck skin:** a harmonic gradient from the collar line (no head motion) to the jaw and
    skull line (full head motion), so the app's head turns twist the neck smoothly.
  - Hair chains (scalp layer pinned to the head, fringe swings at most 65 %) and hem panels.
  - Four influences per vertex.
- **Face:**
  - The painted eye bulges were replaced by rotatable eyeballs with a procedural hazel iris
    matched to the paint, plus socket walls and hidden eyelid shells.
  - The lips were cut on the measured contact line, with inner walls, cavity, teeth and tongue.
  - 76 body channels: 10 CC visemes, the app's expression names and all 52 ARKit-style
    channels. Blinks have 25/50/75/100 % frames.
- **Face polish (skill stage 12):**
  - Corner-aware jaw weights and lip-only visemes and ARKit mouth shapes.
  - The round shapes (oo, pucker, funnel, sh, tight) use a wider, smoother envelope plus
    displacement smoothing, because Arjun's wider mouth creased the decimated cheeks.
  - The mouth cavity is clamped behind the inner lip walls; it had poked through the lip slit.
  - Teeth sit with an overbite, the tongue behind the lower incisors, and the teeth are painted
    in a 256 px mouth atlas.
- **Physics in Unity (`Arjun.rig.json`):** 18 chains.
  - Hair: stiffness 1.8, drag 0.45, gravity 0.12, so the styled spikes hold their shape.
  - Shirt hem: stiffness 0.9, drag 0.45, gravity 0.15.
  - 12 body colliders, shrunk to clear the rest joints.
- **Voice and persona (new for a male companion):**
  - Each roster entry carries a voice key (`female` or `male`).
  - The app sends the voice key and the companion's name with each turn.
  - The local service maps the key to Microsoft Zira or David Desktop. It adds "Your name is
    Arjun." to the system prompt, accepting only a single word from the roster.
  - The picker tooltip now reads "Changes appearance and voice; this chat and draft stay the
    same." Arjun speaks with David; Alita, Meera and Tara keep Zira.

## Results

- **`rig-checks.txt`: 106 PASS** (isolated hidden copy, stopped Editor):
  - renderers and URP materials;
  - all 76 channels deform and reset;
  - blink frames, facing, jaw opens the chin 24 mm;
  - feet planted over 30 s (0.00 mm), relaxed arms, 30 finger joints, gaze;
  - wardrobe roles and tints, buttons untinted;
  - springs: 34 joints and 12 colliders bound, respond 3.9 cm peak, settle 0.000 mm/frame,
    stay outside the colliders;
  - reduced motion;
  - live GPU-skinned draw (101k covered pixels).
- **`app-checks.txt`: 27 PASS** (Play):
  - roster Alita, Meera, Tara, Arjun; picker, remembered selection, draft kept, labels;
  - **Arjun speaks with the male local voice**;
  - springs running, framing bounds 1.99 m, portrait camera coverage (211k opaque pixels);
  - six live face channels drive and reset;
  - wardrobe with shoe colour; cancelled preview not saved; greeting; switching back to Alita
    releases the springs.
- **Face performance:** 17 checks for Arjun in `docs/evidence/m1/face-performance/face-checks.txt`
  (67 across the roster):
  - 14 blinks per idle minute and 19 per minute while talking;
  - Duchenne smile 0.33, inner brows 0.54 on concern;
  - jaw follows articulation × 11°, `mouthClose` seals p/b/m;
  - thinking looks up and away 10.8°.
- **Review video (`arjun-face-performance.mp4`):** 29 s with David's voice, the same three
  turns as the roster video, frame-exact through the runtime face. Peak jaw 7.9°, 10 blinks.
- **Live local-AI turn (`live-turn.txt`, `live-speech-faces.png`):**
  - Reply (happy): "That's amazing! Maybe try something with bold colors or a new texture next."
  - Timing: first audio 7.1 s, complete 13.2 s. This was the first turn after restarting the
    service, so the model was loading.
  - 16 face crops while speaking show open, closed and teeth shapes, the smile and natural
    blinks.
- **Voice end to end:** direct turns through the service with the roster name and voice key:
  - `male`: "My name is Arjun. I'm here to help.", median pitch 93 Hz (David), 0.8 s.
  - `female`: answered as Tara at 199 Hz (Zira).
- **Service boundary checks:** 16 PASS (`node tests/e2e/check-talking-service.mjs`). Unknown
  voices, system voice names and multi-word names are rejected.
- **Regressions after the shared-code changes, all passing:**

  | Suite | Checks |
  |---|---|
  | Meera rig / in-app | 105 / 27 (in-app adds the voice check) |
  | Tara rig / in-app | 106 / 27 |
  | Alita runtime meshes | 143 |
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
  | Speech mouth motion | 19 |

  The Editor ended stopped, with TalkingCompanion saved and clean (8 roots). Alita is still the
  scene default and the owner's character pref (Tara) is unchanged.

Review images:

- `idle-*.png`, `gesture-*.png`, `face-*.png`: baked review renders.
- `app-arjun*.png`: the portrait app.
- `live-speech-faces.png`: live turn face crops.
- `blender/01…10`: source and triangle budget, segmentation, eyes and mouth, mouth interior jaw
  test, skeleton, relaxed pose, neck/collar weights, expression shapes, polished visemes, teeth.

## Known limits

- **Generated rig:**
  - Blendshapes are generated deformations, not artist-sculpted.
  - The round mouth shapes keep a faint crease at the upper-lip corners under strong pucker.
  - A fringe lock hangs at the inner corner of his right eye, as in the source sculpt.
- **Wardrobe:** tints multiply the texture. On his dark-brown shirt they read as darker shades
  (Emerald becomes deep green), not bright colours. Black hair barely changes with hair tints.
- **Voice:** the male voice is Windows David (US English desktop TTS), not a final character
  voice. Lip-sync uses its SAPI visemes.
- **Timing:** first-turn timing includes model load; warm turns answer in under 1 s on this
  machine. This is not device evidence.
- **Device:** no physical-device, mobile GPU, frame-time or memory evidence. The scene now holds
  four loaded rigs, so mobile LOD and memory budgets remain open.
- **Rights:** owner-supplied Tripo output. Commercial-use rights depend on the owner's Tripo
  plan and are not independently verified (Q-010). Local prototype use only until confirmed.
