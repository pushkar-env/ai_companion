# Jaw comparison controls — 2026-09-27

M1 remains partial. Unity 6000.5.9f1, original apps/unity project, existing CCCharacterTest
scene and iOS Notch Device Simulator at 1170×2532. No device installation or network voice.

Implemented a session-only provisional 0–30 degree jaw angle and a 70% bone-only preview.
The existing assist toggle gives morph-only or combined comparisons using V_Open/Jaw_Open.
The new preview clears every facial morph first. Changing the angle stops audio and
restores neutral; restoring 16 degrees resets the prototype default. Values are not
saved to the scene or profile. All controls remain in the portrait scroll area; reset
stays fixed and reachable. `bone-only.png` was visually inspected.

## Passed

- `Companion > Run CC Character Play Mode Checks`: 120 assertions; `play-checks.txt`.
- Twelve new assertions cover measured bone angle, all morph weights remaining zero,
  actual skinned body deformation, geometry restoration, cancellation on adjustment,
  NaN/infinity/out-of-range rejection, zero/max angle and background reset.
- Existing blink corrective, channel binding, sample-clock audio, interrupt/natural
  completion and 20-session cleanup checks still pass.
- `python tools/check-repository.py`: 36 pre-existing asset/package hashes, metadata/GUID
  uniqueness and baseline secret-pattern/ignore checks. Not a comprehensive security audit.
- Editor returned to stopped Play Mode, clean scene, StandaloneWindows64 target and
  portrait settings. Window positions/sizes and Simulator selection unchanged.

## Limitations and next step

`exported-expressions.json` contains the original V_Open/Jaw_Open records and source SHA256.
They show differing bone payloads; no coordinate/unit/rest-space conversion has been
verified. The angle/axis remains a prototype, including the shared approximation for
both channels. Next calibration work must establish that conversion against CC reference
poses before replacing it or adding tongue/head offsets. The 30-degree bound is only
a diagnostic input bound, not a certified anatomically correct range.

Android rebuild was unexecuted at this Editor checkpoint; the later
[171.8 MB build passed](../android/build-20260927-195903/verification.md) and includes
these controls and blink correctives. Physical/perceptual lip-sync, native audio and
iOS validation remain unexecuted/deferred. Provider evaluation remains gated by Q-006;
shipping asset rights remain gated by Q-010. No decisions inferred from unanswered questions.
