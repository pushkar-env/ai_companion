# Exported partial-blink correction — 2026-09-27

The CC JSON defines single-input additive C_BlinkL/C_BlinkR constraints. Deduplicated
linear points are (0,0), (0.5,1), (1,0): no corrective at neutral/full closure, maximum
at half blink. source-rules.json records exact rules and source SHA256. Blink expressions
declare no bone transforms; the earlier auxiliary mesh no-motion observations therefore
do not establish a missing blink bone animation.

Implemented these two curves in the local CC lab's native-channel adapter and added a
Half blink button inside the existing scrolling controls. Do not interpret this as a
complete CC constraint solver. Multi-input, Limit and other Add rules remain unimplemented.
Jaw_Open metadata includes multiple bones, unlike the approximate single-jaw runtime
assist; full coordinate/scale/bone conversion is still outstanding. No guessed transforms
were added for eyes, teeth or tongue.

**Passed:** 108 Play Mode assertions on the existing 1170×2532 portrait Simulator profile.
Ten new assertions check both corrective bindings, peak weight, actual baked-vertex
movement, zero correction at full closure, and neutral reset. Existing audio, interruption,
completion, 20-cycle cleanup and portrait reachability checks passed. See play-checks.txt.

**Passed:** build subset preserves 32 required names and 135 exact frames; 1,373 unused
mesh shapes omitted. See subset-checks.txt. Main source/imported meshes remain complete.

Corrective deltas affect 1,683 left / 1,699 right body vertices, with maximum local
displacement about 0.00487 units. Same-named Brows/Tear_Ducts corrective deltas are zero;
do not fabricate deformation on those surfaces. half-blink.png and
half-blink-without-correction.png are actual Play Mode captures of the same half-blink
pose with only the two corrective weights changed, visually inspected. The change adjusts
the eyelid contour; neither capture certifies final skin/eye shading or perceptual quality.

Editor layout and initial stopped Play Mode state were preserved. No source FBX, material,
scene or asset GUID change. Android APK 20260926-192707 (169.4 MB) predates this
runtime change. The later [171.8 MB rebuild passed](../android/build-20260927-195903/verification.md)
and includes it; physical validation remains unexecuted.
Production eye/teeth/tongue fidelity, multi-channel constraints and canonical-52 calibration
remain incomplete. Synthetic audio is unchanged; no real voice or microphone added.
