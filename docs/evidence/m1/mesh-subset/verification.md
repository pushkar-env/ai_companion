# CC diagnostic build mesh subset

Current source update, 2026-09-27: partial-blink correction adds C_BlinkL/C_BlinkR.
Latest checks.txt has 32 names / 135 exact frames / 1,373 omitted shapes. The 30-name,
129-frame numbers below describe the original APK optimization experiment, whose package
measurement remains valid for that built revision. See ../blink-correction/verification.md;
the new runtime revision has not been rebuilt into an APK yet.

2026-09-26, Unity 6000.5.9f1. Scoped to the CCCharacterTest development scene in Android
or iOS builds. The editor scene and imported FBX keep the full rig. No project relocation,
source model rewrite, new mesh asset GUID, renderer removal or package change.

The previous successful Android log reported 842.9 MB of uncompressed mesh data (88.7%
of user asset bytes), compared with 75.8 MB of textures. femaleCC.Fbx accounted for about
843.0 MB. The mesh category is therefore the first size target, not lower texture quality.
Baseline: artifacts/android-test/20260926-184350/unity-build.log, Build Report section.

CCDiagnosticMeshPolicy clones each referenced skinned mesh during scene processing and
keeps only the lab's 29 manual native controls plus V_Tongue_Raise (synthetic kk cue).
It preserves every frame and its weight/deltas, base geometry, skinning and materials.
Authored nonzero values for discarded shapes cause a build error rather than silent pose
loss. Non-development lab builds remain rejected. The policy does not apply to other
scenes or production avatars. Future controls must extend the retained set before use.

**Passed:** Companion > Check CC Build Mesh Subset. See checks.txt. All 30 required names
exist across the rig; 129 retained shape frames match exactly (positions, normals,
tangents, frame count and frame weights). Base vertices/normals/tangents, all UV channels,
bone weights, bind poses, bounds and submesh indices match. 1,379 unused mesh shapes are
omitted. Eight mesh renderers were checked; meshes without blendshapes are untouched.

The FBX body retains 27 of its 348 shapes; the remaining required controls live on other
meshes. This is an exact subset for this diagnostic's current controls, not a complete
canonical-52 profile or production calibration. Triangle count/material slots remain
unchanged and above the production target. Device FPS, memory, physical appearance and
audio are still unverified. Removing unused deltas is not mesh decimation or an LOD system.

Run the normal tools/build-android-test.ps1 to apply the policy in a fresh isolated build.
Final package size and package inspection are recorded in the associated Android build
evidence after completion. Original assets/GUID preservation and Editor layout checks are
separate from these geometry equivalence checks.

Completion recorded 2026-09-27: [Android build and package checks passed](../android/build-20260926-192707/verification.md).
APK 169,373,218 bytes, 69.94% below the baseline. package-comparison.json records exact
ZIP sizes; baseline-assets.txt and reduced-assets.txt preserve Unity's rounded categories.
