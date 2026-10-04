# New local chat verification — 2026-10-03

Environment: Unity 6000.5.9f1, existing TalkingCompanion scene, portrait Simulator
1170×2532, installed local qwen2.5:7b and Windows speech. No new model/package/provider.

Passed:

- 14 Editor checks in `editor-checks.txt`: reset availability during speech; removal of
  context, reply, draft, audio/cues, queued work and prior transcript; Retry invalidation;
  no late playback/transcription/generation; actual next-request history is empty;
  fresh real AI reply and speech complete; portrait and touch-target bounds.
- 19 existing real Editor conversation checks passed again: facial expressions, audio,
  lip motion, cancellation, Retry and portrait layout. Results in the sibling
  `talking-companion/editor-checks.txt`.
- Repository checks: all 36 pre-existing asset/package hashes preserved, metadata present,
  GUIDs unique and baseline secret-pattern/ignore checks passed.
- `git diff --check` passed. Screenshot `ready.png` visually inspected: header button,
  character, draft, microphone and conversation controls remain visible in portrait.

Initial failure: the local service was running but the Ollama engine was stopped. The
real-reply test and standalone streaming probe failed. Started the already-installed
local engine (no download/account), reran the reset test and all 14 checks passed.
The 12 service boundary checks also passed during diagnosis.
Final Console query contained only the original unavailable-engine test failure at
12:23:50; no subsequent error/exception was reported. Exited Play and preserved the clean
scene and all recorded window rectangles (`editor-state.txt`).

Unexecuted: reset during physical microphone recording, OS permissions/disconnects,
subjective speech quality, Hindi and mobile builds/device checks. Tests submitted generated
silence for transcription and never opened a microphone. No production data deletion,
retention or forensic memory-erasure claim; the model remains loaded independently.

Reproduce: enter Play in TalkingCompanion, then **Companion > Run New Chat Checks**.
Manual check: start a reply or recording, click **New chat**, then submit a new message.
Prior messages/draft should disappear, audio should stop, and Retry should be disabled
until a new prompt is submitted. Selected microphone is retained.
