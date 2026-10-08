# Alita wardrobe and attentive idle — 2026-10-06

Unity 6000.5.9f1, Windows Editor, original TalkingCompanion scene. Open **Style** in Play
mode, preview garments/colors, then Save look or Cancel. Save stays on this device.

- Two tops (relaxed tee, sleeveless shell) and two bottoms (denim shorts, midi skirt),
  plus original dress. Independent clothing, hair and sneaker tints; turn preview.
- 36 baked-pose screenshots: four combinations × front/side/back × 0/7/20 seconds.
  `contact.png` compares all combinations at seven seconds. Smooth normals, seam overlap
  and chest clearance were corrected after first review. Originals remain intact.
- `checks.txt`: three automated outfit/gaze checks, including 50 switches with no added
  garment renderers, attention return and reduced-motion head stability.
- `ui-checks.txt`: eight live try-on checks, including Cancel restoring appearance,
  preview not saving, Save persistence, reopen, and portrait bounds at 390×844/360×640.
- `wardrobe-ui.png`, `wardrobe-compact.png`, `saved-outfit.png`: actual UI captures.
- `idle-live.mp4`: 528 actual runtime frames at 12 fps (44 seconds); `motion-review.png`
  samples the recording. Hip/hand motion recorded in `runtime-motion.txt`.
- Existing BodyIdleChecks rerun: 34 passes including all fingers, foot planting, restoration.

Baked pose captures are deterministic diagnostic samples, not live video. Early rapid
unbaked captures showed stale GPU skinning and were replaced. No Editor window, docking,
Game-view selection or orientation helper was used. Device performance, arbitrary body
morphs, entitlement enforcement and shipping wardrobe/catalog remain unverified.

Final regression: **112 checks passed**, including 56 chat/layout and 11 actual local
speech/replay checks. Copies are body-regression.txt, chat-regression.txt and
speech-regression.txt. The initial speech run found Ollama stopped; the installed
local service was started and qwen3:8b availability confirmed before the passing rerun.
No models were downloaded. Editor returned to stopped Play state.

Preservation verified: Play=False, scene clean, five roots, Portrait orientation, no
scene/project-setting diff. Console retains the historical Ollama-unavailable error
from the first speech run; the later 11-check rerun passed. No new runtime warnings
were reported. Git diff whitespace check passed.
