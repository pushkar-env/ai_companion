# Speech timing diagnostic — 2026-10-04

Unity 6000.5.9f1, existing TalkingCompanion scene, 1170×2532 portrait Simulator,
local qwen2.5:7b inference and Windows speech. Run through the project-pinned Companion
MCP; no microphone capture or cloud request. Explicit menu: Companion > Run Speech
Timing Checks in Play. The check resets chat and uses two synthetic two-sentence turns.

11 checks passed in `editor-checks.txt`. Actual AudioSource sample progression and jaw
motion were observed for both turns; Replay preserved the original metrics, New chat
reset them, and cancellation left completion unset. `timings.csv` contains only timings
and chunk counts, no transcripts/audio. First playback: 0.990 and 0.935 seconds; maximum
observed inter-clip waiting: 0.001 seconds each. No latency threshold was asserted.

Timing starts at submission to the local service. First audio means AudioSource.Play
was called, not that the sound reached a listener. Inter-clip wait starts when the main
thread observes the previous clip stopped and ends on starting the next clip; polling
can miss a frame's delay and silence inside a clip is excluded. Full text is generated
before text delivery. Two local samples are not p50/p95, cold-start or PERF-01 release
acceptance. Physical speaker/Bluetooth latency, measured AV offset, real microphone
accuracy, Hindi speech and mobile resource/latency tests remain unexecuted.

Scene stayed clean and was returned to stopped Play state. Persistent Editor window
rectangles and portrait Simulator size preserved (`editor-state.txt`). Repository hash/
GUID/metadata checks and `git diff --check` passed. No new packages or asset replacements.
