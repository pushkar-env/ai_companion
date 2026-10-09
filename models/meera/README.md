# Meera source files

- `source/meera-tripo-source.glb` — owner-supplied Tripo export (unchanged copy of
  `3d character model.glb`, 2026-10-08). Commercial rights follow the owner's Tripo terms;
  not independently verified (QUESTIONS.md Q-010).
- `Meera_Rig.blend` — the current editable rig: collection `Companion_Work` (Meera_Rig,
  Meera_Body, Meera_Eyes, Meera_Mouth) plus a review camera. Since the face polish
  (2026-10-09, ADR-073) it holds only the rig; the earlier full working file with the
  `Source_Tripo` high-poly import and the pre-surgery `Backup` mesh is the 2026-10-08
  version of this file in git history (commit 1252d9a).

Face polish (ADR-073, `.claude/skills/tripo-character-rig/blender/12_face_polish.py`):
corner-aware jaw skin weights (the lip halves meet at 0.5 at the corners, so the jaw opens
an oval), lip-only viseme and ARKit mouth shapes (the app's jaw bone does the opening),
tongue lowered behind the incisors, lower incisors raised to sit just behind the uppers,
teeth arches rebuilt with premolars, and a 256 px mouth atlas with painted teeth.

Re-export for Unity (same settings as the shipped FBX): select Meera_Rig, Meera_Body,
Meera_Eyes and Meera_Mouth, set every shape key to 0 and the armature to Rest Position, then
FBX export with Selected Objects, Armature + Mesh, Apply Scalings "FBX All", Forward -Z,
Up Y, Only Deform Bones, no leaf bones, no baked animation, no modifiers (keeps shape keys),
path mode Strip. Copy the FBX to `apps/unity/Assets/Companion/Imported/Meera/Meera.fbx`
(and `Meera_Mouth.png` to `Textures/` if the mouth atlas changed) and run
`Companion/Characters/Import Meera` in the stopped Editor with TalkingCompanion open. The
import is re-runnable and keeps asset GUIDs. Shape keys named `<shape>__fNN` are folded
into in-between frames of `<shape>` by `MeeraModelPostprocessor` during FBX import.

Evidence and decisions: `docs/evidence/m1/meera/README.md`, ADR-071;
face performance: `docs/evidence/m1/face-performance/README.md`, ADR-073.
