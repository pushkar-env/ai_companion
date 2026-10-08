# Articulated idle hands — 2026-10-06

The first body idle left every finger in its straight bind pose. This revision owns
all 30 finger/thumb joints as part of the same rest-pose and restoration system.

- Progressive resting curl: index is least curled, pinky most curled; each digit has
  distinct proximal, middle and distal angles.
- Thumb opposition brings the thumb towards the index instead of leaving it splayed.
  The initial thumb attempt was revised after close-up review.
- Wrist extension/roll, asymmetric elbow positions and staggered finger release/settle
  coordinate with breathing and a slow asymmetric hip shift.
- A common 24-second cycle replaces unrelated body frequencies. Feet remain planted;
  facial/speech controls remain separate. Reduced motion retains the articulated rest pose.

## Visual review

![Both hands, front and side, at four phases](review-grid.png)

Inspected both hands from front and side at 0, 6, 12 and 18 seconds. Checked palm direction,
thumb splay, finger shape/spacing and dress clearance. No obvious intersections in these
sampled views; this is visual review of the current outfit, not general collision proof.

[Close-up motion](hand-motion.mp4) is a 24-second diagnostic render of the same pose
function at 12 samples/second: left hand front, right hand side. It is not a fabricated
AI animation. [Live app preview](app-idle.mp4) records 96 actual Unity update frames using
a temporary offscreen panel. Editor layout/Game-view selection was not changed.

## Verification

- 34 rig checks passed: all 30 finger/thumb joints leave bind pose and animate; 24-second
  pose closure; foot stability; no root/accumulated drift; source pose restoration.
  See rig-checks.txt.
- Live runtime movement passed; see runtime-motion.txt.
- 11 real speech/replay checks passed with the revised idle; see replay-checks.txt.
- Initial Play state was running and final Play state remains running. Scene clean,
  five roots, portrait orientation; no scene/ProjectSettings diff; Console errors/warnings
  empty. See preservation.txt.

The current character/outfit, face assets, scene and existing GUIDs are preserved. This
is a procedural idle on the supplied rig; it does not introduce a new animation provider
or motion-capture asset. New outfits and expressive gestures require their own review.
