# Tara source files

- `source/tara-tripo-source.glb` — owner-supplied Tripo export (unchanged copy of
  `girl_character_model.glb`, 2026-10-09, SHA-256 `1fd4987f…ff976`). Commercial rights follow
  the owner's Tripo terms; not independently verified (QUESTIONS.md Q-010). "Tara" is a
  placeholder name; the owner did not name the character.
- `Tara_Rig.blend` — the current editable rig: collection `Companion_Work` (Tara_Rig,
  Tara_Body, Tara_Eyes, Tara_Mouth) plus a review camera. The armature has bone collections
  Deform, Face, Dynamics and Controls; leg/arm IK, knee/elbow poles and the eye look target
  are Blender-only. Since the face polish (2026-10-09, ADR-073) the file holds only the rig;
  the full pipeline working file with the `Source_Tripo` high-poly import and the pre-surgery
  `Backup` mesh stays outside the repository at
  `D:/Blender/Companion_Character_Rig_20261008_Tara/companion-character-rig-tara-20261008-01.blend`.

Face polish (ADR-073, `.claude/skills/tripo-character-rig/blender/12_face_polish.py`):
corner-aware jaw skin weights, lip-only viseme and ARKit mouth shapes, tongue shortened and
lifted behind the lower incisors, lower incisors raised just behind the uppers, teeth arches
rebuilt with premolars, and a 256 px mouth atlas with painted teeth.

Re-export for Unity (same settings as the shipped FBX): select Tara_Rig, Tara_Body,
Tara_Eyes and Tara_Mouth, set every shape key to 0 and the armature to Rest Position, then FBX
export with Selected Objects, Armature + Mesh, Apply Scalings "FBX All", Forward -Z, Up Y,
Only Deform Bones, no leaf bones, no baked animation, no modifiers (keeps shape keys), path
mode Strip. Copy the FBX to `apps/unity/Assets/Companion/Imported/Tara/Tara.fbx` (and
`Tara_Mouth.png` to `Textures/` if the mouth atlas changed) and run
`Companion/Characters/Import Tara` in the stopped Editor with TalkingCompanion open. The
import is re-runnable and keeps asset GUIDs. Shape keys named `<shape>__fNN` are folded into
in-between frames of `<shape>` during FBX import.

Then run `Companion/Characters/Check Tara Rig` (stopped) and `Check Tara In App` (Play).
Evidence and decisions: `docs/evidence/m1/tara/README.md`, ADR-072;
face performance: `docs/evidence/m1/face-performance/README.md`, ADR-073.
