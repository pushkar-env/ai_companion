# Full-body glass conversation UI

Implementation/verification: 2026-10-05 to 2026-10-06, Unity 6000.5.9f1 on Windows.
This is the actual TalkingCompanion runtime, not the generated concept illustration.

Final state: Editor stopped, TalkingCompanion scene clean with five roots, portrait
orientation preserved; Console errors/warnings empty. No scene or ProjectSettings diff.
See [preservation.txt](preservation.txt). Existing user/backend changes were preserved.

![390x844 runtime](layout-0.png)

Implemented: original full-body Alita over the generated evening room, transparent camera
compositing, smoky native glass surfaces, expandable chat drawer, incoming/outgoing bubbles,
local timestamps, multiline composer, vector microphone/send icons, contextual Stop/Retry/
Replay, measured recording level/timer, review-before-send and modal Settings. Settings
includes larger messages (150%), reduced transparency, confirmed New chat and existing
local setup/microphone/history tools. New chat/Retry preserve intended conversation semantics;
retry no longer appends a duplicate user bubble. Existing scene/model GUIDs remain intact.

## Verification

- `Companion/Run Conversation Polish Checks`: **56 passed** in [checks.txt](checks.txt).
  Uses the real runtime view and camera with a temporary cloned panel/render target.
  No Game-view resolution/selection or Editor-window changes.
- `Companion/Run Reply Replay Checks`: **11 passed**, actual local generated speech,
  cached replay, facial movement, Stop and context preservation. The conditional replay
  action check now waits for the next UI layout pass before checking geometry.
- `Companion/Run Local Voice Input Checks`: **11 passed**, synthetic PCM through actual
  local transcription, editable recognized text, no automatic send and cancellation.
  This does not test the owner's microphone/acoustic quality.
- `Companion/Run Microphone Selection Checks`: **12 passed**, selected-device persistence,
  disconnect/reconnect policy, no automatic recording and controls in Settings.
- `Companion/Run New Chat Checks`: **14 passed**, resetting during real generated speech,
  transcription and generation; late callbacks rejected, new request context empty and
  the next real conversation completes. New chat control measured inside Settings.

Replay, voice, microphone and New chat evidence is copied alongside this README. The initial
unresolved-layout NaN camera issue was fixed and did not recur in the final run.

## Captures

| File | Runtime state |
|---|---|
| layout-0.png | 390x844, 24px top/bottom insets |
| layout-1.png | 360x640, 24px insets |
| layout-2.png | Expanded chat at 390x844 |
| layout-3.png | 390x844 with simulated 280px keyboard clearance |
| layout-4.png | 360x640 with simulated 240px keyboard clearance |
| layout-5.png | 150% message text at 360x640 |
| layout-6.png | Busy/Stop UI fixture (not live speech in this capture) |
| layout-7.png | Modal Settings |

Keyboard screenshots reserve the correct area but do not draw a fake keyboard. The
companion becomes small on the tightest layout to keep the entire body visible.
Screenshots were visually inspected; text overflow in the transcript is scrollable.
Final capture review also caught and corrected a Windows text-encoding issue in UI labels.

## Limits and next work

Glass is tinted transparency, not a live blur. Room is a 2D plate with a simple contact
shadow, not an orbitable 3D environment. Avatar pose/animation/material quality remains
the existing asset quality. No realtime voice, production sign-in, real-user persistence,
new store functionality or provider policy is implied.

Physical Android/iOS keyboard/IME, microphone permissions/routing, 200% text, Hindi font
shaping, screen-reader semantics and frame/memory/thermal profiling remain unverified.
Next: native device input/accessibility and render-budget validation, then complete
production navigation and approved identity/provider integrations. M1/M2 remain partial.
