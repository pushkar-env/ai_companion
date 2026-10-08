# Relaxed full-body idle — 2026-10-06

The old talking scene had fixed lowered arms and only head/facial motion; no Animator
or body clip was attached. CompanionBodyIdle now supplies a procedural breathing/weight
shift layer on the existing CC rig, with relaxed arms, soft elbows, shoulder and wrist
settling. The legs compensate for hip motion while feet stay planted. Head/jaw/facial
channels remain separate for the existing speech animation.

![Runtime relaxed pose](relaxed-pose.png)

[Runtime motion preview](idle.mp4): 96 actual offscreen Unity frames, encoded at 12 fps.
No generated animation or replacement character imagery. The room and outfit are unchanged.

## Evidence

- `Companion/Run Full Body Idle Checks`: **31 passed** on an isolated clone of Alita.
  30 seconds sampled at 60 Hz: 16 joints change, feet stay within 0.5 mm, foot orientation
  stays fixed, no root/accumulated drift, actual skin vertices deform and original pose
  restores on disposal. See rig-checks.txt. Tiny per-frame Quaternion.Angle values round
  to zero at Unity's precision; the continuity check is a bound, not an angular-speed claim.
- `Companion/Run Conversation Polish Checks`: **56 passed**, actual full-body runtime
  across portrait, compact keyboard, expanded chat and larger text. See portrait-checks.txt.
- `Companion/Capture Full Body Idle Preview`: **passed**, actual Update motion measured
  over the recording: hip range 0.02302 m, hand range 0.02141 m. See runtime-motion.txt.
- **Reduced idle motion passed**: hip/head hold still for 750 ms after the preference
  change; original preference restored. The relaxed pose remains instead of reverting to A-pose.
- `Companion/Run Reply Replay Checks`: **11 passed** with the body idle enabled: actual
  generated speech, cached replay, jaw movement, Stop and conversation preservation.
  See replay-checks.txt. Console errors/warnings empty after verification.

Idle sampling restores cached local transforms before applying each absolute-time pose;
it does not accumulate rotations. A modest breathing/weight shift is intentional. This
is a stationary procedural idle, not a motion-captured performance or walking controller.
Physical mobile performance and other outfit/rig compatibility remain unverified.
