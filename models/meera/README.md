# Meera source files

- `source/meera-tripo-source.glb` — owner-supplied Tripo export (unchanged copy of
  `3d character model.glb`, 2026-10-08). Commercial rights follow the owner's Tripo terms;
  not independently verified (QUESTIONS.md Q-010).
- `Meera_Rig.blend` — editable rig built in the live Blender 5.2.2 session. Collections:
  `Companion_Work` (Meera_Rig, Meera_Body, Meera_Eyes, Meera_Mouth), `Source_Tripo`
  (original high-poly import), `Backup` (segmented mesh before face surgery), `Review`
  (review camera and helpers, not exported).

Re-export for Unity (same settings as the shipped FBX): select Meera_Rig, Meera_Body,
Meera_Eyes and Meera_Mouth, set the armature to Rest Position, then FBX export with
Selected Objects, Armature + Mesh, Apply Scalings "FBX All", Forward -Z, Up Y, Only Deform
Bones, no leaf bones, no baked animation, no modifiers (keeps shape keys), path mode Strip.
Copy the FBX to `apps/unity/Assets/Companion/Imported/Meera/Meera.fbx` and run
`Companion/Characters/Import Meera` in the stopped Editor with TalkingCompanion open. The
import is re-runnable and keeps asset GUIDs. Shape keys named `<shape>__fNN` are folded
into in-between frames of `<shape>` by `MeeraModelPostprocessor` during FBX import.

Evidence and decisions: `docs/evidence/m1/meera/README.md`, ADR-071.
