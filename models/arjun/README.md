# Arjun source files

- **`source/arjun-tripo-source.glb`**: the owner-supplied Tripo export, an unchanged copy of
  `3d boy model.glb` (2026-10-09, SHA-256 `5dd7ab9c…834d57`).
  - Commercial rights follow the owner's Tripo terms and are not independently verified
    (QUESTIONS.md Q-010).
  - "Arjun" is a placeholder name; the owner did not name the character.
  - He is presented as an adult: scaled to 1.78 m including hair, with adult body proportions.
- **`Arjun_Rig.blend`**: the editable modular rig (ADR-075), with packed textures. Collection
  `Companion_Work` holds:
  - the armature `Arjun_Rig`;
  - the base body `Arjun_Body` (skin, hair, mouth/socket walls, the 76 face channels), plus
    `Arjun_Eyes` and `Arjun_Mouth`;
  - six garments in five meshes: `Arjun_Shirt_Classic` (brown shirt, signature look),
    `Arjun_Shirt_Chambray` (open chambray shirt, white tee and buttons), `Arjun_Trousers` (one
    fitted mesh for the grey trousers and the olive chinos), `Arjun_Sneakers` and `Arjun_Watch`.

  More details:
  - **Armature:** bone collections Deform, Face, Dynamics and Controls. Garment spring chains
    (`Shirt_*`, `Chambray_*`) sit in Dynamics. `Share_L/R_Upperarm` (Deform) carry a Copy Rotation
    (local, 0.5) that previews the app's share-joint driver. Leg/arm IK, knee/elbow poles and the eye look
    target are Blender-only.
  - **Working files (outside the repository):**
    - the original rig build: `D:/Blender/Companion_Character_Rig_20261009_Arjun/`;
    - the wardrobe build, with checkpoints and exports:
      `D:/Blender/Companion_Outfits_20261009_Arjun/arjun-outfits-20261009-01.blend`.

Built with the project skill (`.claude/skills/tripo-character-rig`): stages 1–12 for the rig and
face, and stage 13 (`blender/13_modular_garments.py` and `textures/*.py`) for the modular wardrobe.

## Re-export for Unity

Use the same settings for every file:

- select the armature and the meshes for that file;
- every shape key at 0, the armature in Rest Position;
- FBX export with Selected Objects, Armature + Mesh, Apply Scalings "FBX All", Forward -Z, Up Y,
  Only Deform Bones;
- no leaf bones, no baked animation, no modifiers (keeps shape keys), path mode Strip.

The deform flags pick the bones for each file:

| File | Meshes | Deform bones |
|---|---|---|
| `Imported/Arjun/Arjun.fbx` | Arjun_Body, Arjun_Eyes, Arjun_Mouth | CC_Base_* + Hair_* |
| `Imported/Arjun/Wardrobe/Arjun_Shirt_Classic.fbx` | Arjun_Shirt_Classic | CC_Base_* + Shirt_* + Share_* |
| `Imported/Arjun/Wardrobe/Arjun_Shirt_Chambray.fbx` | Arjun_Shirt_Chambray | CC_Base_* + Chambray_* + Share_* |
| `Imported/Arjun/Wardrobe/Arjun_Trousers.fbx`, `Arjun_Sneakers.fbx`, `Arjun_Watch.fbx` | one mesh each | CC_Base_* |

Before exporting:
- **Weights:** drop weights to bones a garment does not export, and refill those vertices from
  their neighbours.
- **N-gons:** triangulate them.

Write the spring chains of each shirt to `Wardrobe/<File>.item.json` (the `Arjun.rig.json` chain
format), plus its `shares`. These are the half-rotation shoulder bones `Share_L/R_Upperarm`
(clavicle children with the upper arm's rest pose); the app turns them by half the upper arm.
Write the hair chains and colliders to `Arjun.rig.json`. The garment textures go to
`Textures/`:

- `Arjun_Chambray_{BaseColor,Normal}.png`
- `Arjun_Trousers_Grey_BaseColor.png`, `Arjun_Chinos_Olive_BaseColor.png`, `Arjun_Trousers_Normal.png`
- `Arjun_Watch_{BaseColor,Normal}.png`

Then run `Companion/Characters/Import Arjun` in the stopped Editor with TalkingCompanion open. The
import:
- rebuilds the materials;
- binds every garment to the character's bones by name and grafts the chain bones;
- writes the wardrobe profile and the springs;
- saves the scene.

It is re-runnable and keeps asset GUIDs. Shape keys named `<shape>__fNN` are folded into
in-between frames of `<shape>`. `Arjun_BaseColor.jpg` and `Arjun_Normal.png` are the GLB's
original image bytes (no re-encode).

Then run `Companion/Characters/Check Arjun Rig` (stopped) and `Check Arjun In App` (Play).
Evidence and decisions:
- `docs/evidence/m1/arjun/README.md` (ADR-074);
- `docs/evidence/m1/arjun/wardrobe/README.md` (ADR-075).
