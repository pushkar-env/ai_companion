# Arjun source files

- `source/arjun-tripo-source.glb` — owner-supplied Tripo export (unchanged copy of
  `3d boy model.glb`, 2026-10-09, SHA-256 `5dd7ab9c…834d57`). Commercial rights follow the
  owner's Tripo terms; not independently verified (QUESTIONS.md Q-010). "Arjun" is a placeholder
  name; the owner did not name the character. He is presented as an adult (scaled to 1.78 m
  including hair, adult body proportions).
- `Arjun_Rig.blend` — the editable rig only: collection `Companion_Work` (Arjun_Rig, Arjun_Body,
  Arjun_Eyes, Arjun_Mouth) plus the review camera, with packed textures. The armature has bone
  collections Deform, Face, Dynamics and Controls; leg/arm IK, knee/elbow poles and the eye look
  target are Blender-only. The full pipeline working file (with the `Source_Tripo` high-poly
  import, the pre-surgery `Backup` mesh and review helpers) stays outside the repository at
  `D:/Blender/Companion_Character_Rig_20261009_Arjun/companion-character-rig-arjun-20261009-01.blend`.

Built with the project skill (`.claude/skills/tripo-character-rig`) including the stage-12 face
polish. Corner-aware jaw skin weights, lip-only viseme and ARKit mouth shapes, teeth placed with
an overbite and the tongue behind the lower incisors, and a 256 px mouth atlas with painted teeth.

Re-export for Unity (same settings as the shipped FBX): select Arjun_Rig, Arjun_Body, Arjun_Eyes
and Arjun_Mouth, set every shape key to 0 and the armature to Rest Position, then FBX export
with Selected Objects, Armature + Mesh, Apply Scalings "FBX All", Forward -Z, Up Y, Only Deform
Bones, no leaf bones, no baked animation, no modifiers (keeps shape keys), path mode Strip.
Copy the FBX to `apps/unity/Assets/Companion/Imported/Arjun/Arjun.fbx` (and `Arjun_Mouth.png` /
`Arjun_Eyes.png` to `Textures/` if they changed) and run `Companion/Characters/Import Arjun` in
the stopped Editor with TalkingCompanion open. The import is re-runnable and keeps asset GUIDs.
Shape keys named `<shape>__fNN` are folded into in-between frames of `<shape>` during import.
`Arjun_BaseColor.jpg` and `Arjun_Normal.png` are the GLB's original image bytes (no re-encode).

Then run `Companion/Characters/Check Arjun Rig` (stopped) and `Check Arjun In App` (Play).
Evidence and decisions: `docs/evidence/m1/arjun/README.md`, ADR-074.
