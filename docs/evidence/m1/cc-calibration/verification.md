# Native CC morph calibration baseline

2026-09-26, Unity 6000.5.9f1, original apps/unity project. Run **Companion > Audit CC
Mesh Deformation** outside Play Mode. The audit instantiates the imported FBX in a
disposable preview scene, disables animation, zeros morphs, and bakes skinned vertices
in world coordinates. It never saves/modifies the source FBX or open scene.

Result: **90 probes across 30 controls passed** binding, finite-position and neutral
reset checks. All controls moved at least one mesh at normalized weights 0.25, 0.5 and
1.0. Reset displacement was zero in this run; tolerance is one micrometre. Coverage
includes 29 manually exposed controls and V_Tongue_Raise, used by the synthetic kk cue.
See audit.txt, deformation.csv and mesh-coverage.csv.
After the audit, editor-state.txt recorded the original scene with Dirty=False, Play=False
and StandaloneWindows64 target. All window rectangles match the previous layout baseline.
All 121 root model file hashes, 36 original asset/package hashes and GUID checks passed.

This strengthens the previous weight-setting assertions: matching names alone do not
prove deformation. For both Eye_Blink channels, five renderers expose the name, but only
CC_Base_Body moved above the threshold. Brows, EyeOcclusion, Tear_Ducts and TearLine did
not move for these probes. This is an observation, not proof that each should deform;
eye surface/occlusion fidelity needs a close-up review and the CC bone-expression data.

Morph-only V_Open maximum displacement was about 9 mm at full weight; Jaw_Open about
20 mm. The runtime jaw-bone assist remains an independent provisional rotation. Do not
derive final gains or bone transforms from maximum displacement alone. No automatic
gain changes, mesh edits, bone conversion, canonical-52 approval or perceptual speech
calibration were made. The audit is Editor-only and was added after the Android build
source snapshot; the player's runtime code is unchanged by this work.

Next calibration work: compare CC expression metadata and anatomical eye directions,
review eye/teeth/tongue surfaces at extreme poses, then create versioned gains/clamps
and viseme combinations with English/Hindi recordings after voice rights/provider approval.
