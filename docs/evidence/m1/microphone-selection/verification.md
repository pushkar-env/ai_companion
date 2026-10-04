# Microphone selection — 2026-10-03

Unity 6000.5.9f1, TalkingCompanion in existing 1170×2532 portrait Simulator.

Passed:

- 12 selection checks (`editor-checks.txt`): remembered name versus enumeration order,
  reordering, disconnect without silent switch, placeholder rejection, reconnection,
  explicit replacement, empty list, explicit choice after empty startup, fresh-model
  restoration, actual refresh preserving draft/selection without recording, and portrait
  touch-target/width bounds. Disconnect/reconnect cases use synthetic device lists.
- 11 real voice-input regression checks using generated PCM; editable transcription,
  no automatic recording/send and cancellation still pass.
- Selected a different enumerated input through the actual dropdown, verified its saved
  preference, restarted Play and verified restoration. Restored the original preference
  afterward. No capture. Final Console error/exception query empty; Play stopped, scene
  clean and Editor window rectangles preserved (`editor-state.txt`).
- Repository preservation: 36 original hashes, metadata presence and unique GUIDs.
  `git diff --check` passed. Screenshot inspected for portrait controls.

No physical microphone was opened. Actual USB unplug/replug, permission failures,
recognition accuracy and multiple identical device names remain unverified. Selection
uses the exact device names exposed by Unity. Only the explicitly selected name is saved
locally; no audio/transcript or device-name transfer is introduced.

Reproduce: Play, **Companion > Run Microphone Selection Checks**. Manually choose your
intended microphone, stop/restart Play, and verify the choice remains. Connect/disconnect
the microphone and click Refresh; select a replacement explicitly when needed.
