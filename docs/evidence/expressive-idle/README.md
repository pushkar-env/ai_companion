# Expressive idle review — 2026-10-06

Unity 6000.5.9f1, Windows Editor, existing Alita and TalkingCompanion scene.

Five in-place gestures: small foot adjustment, hip turn, shoulder roll, side stretch,
and two-arm yawn. Unequal quiet intervals, mirrored variants, variable strength/tempo
and a recent-gesture exclusion prevent a fixed repeating playlist. The first yawn is
at least 80 seconds after launch; later yawns also have an 80-second minimum cooldown.
Side stretches have a 35-second cooldown. The motion library is finite, not infinitely
unique animation. This is procedural animation, not motion capture.

- `checks.txt`: 29 isolated checks including sole lift/support contact, continuous
  hand/foot trajectories, exact neutral joins, ten-minute varied schedule, conversation
  takeover, reduced motion and complete rig restoration. Seed 8416 makes this reproducible.
- `gesture-review.png`: all five gesture peaks, front/side/back, tee and shorts.
- `outfit-review.png`: raised-arm review across all five supported outfits.
- 75 individual screenshots: five gestures × five outfits × three angles. Each sampled
  skin is baked before rendering to avoid stale GPU skin matrices in rapid captures.
- Original 34 body/finger checks rerun successfully; relaxed baseline sampler unchanged.
- Live capture explicitly cues every gesture for review, using actual Update, face,
  camera and UI. This demonstration order is not the production random schedule.
  `gestures-live.mp4` is the 46.5-second timed capture (458 frames).
  `live-checks.txt` records five completed cues and full-body framing; timestamped source frames remain
  under ignored artifacts/expressive-idle. Final encoding preserves captured timing.

Large gestures blend out when conversation starts; feet finish their landing. Yawn jaw
and eyelids yield immediately to conversation; head tilt eases down with the body.
Reduce idle motion holds the relaxed pose and retains normal blinks. Camera framing
reserves raised-hand clearance rather than zooming on each gesture.

This validates the existing Alita body/outfits in desktop Editor. New garments, other
body shapes, device performance, locomotion and large dance/exercise gestures need their
own review. No scene/rig/source asset or Editor-layout change is required.

56 portrait/chat regression checks passed; saved in chat-checks.txt. The idle preview
uses the current locally selected outfit and does not save an appearance change.

Final result: **135 passing checks** (29 motion/schedule, 34 baseline, 5 live gesture/
framing, 56 chat/layout, 11 real local speech/replay). Console errors and warnings empty;
Git whitespace check passed. Editor restored to stopped; original portrait scene and
asset GUIDs preserved. Physical-device performance remains unverified.
