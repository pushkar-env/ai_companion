# Local reply replay — 2026-10-03

Unity 6000.5.9f1, TalkingCompanion, existing portrait Simulator. Cached data is the last
completed local reply's validated PCM/viseme frames, bounded by the existing stream limit
and three frames. No audio file or additional AI/TTS request is made for Replay.

11 checks passed (`editor-checks.txt`): empty-chat disablement, completion enabling,
cached playback without a turn request, every original sentence played, exact reply
retained, unchanged conversation context/transcript, sample-clock audio and jaw motion,
Stop and replay availability, no false interruption label on the completed exchange,
portrait action bounds, and release on New chat. Screenshot `controls.png` inspected;
all four action buttons remain visible and touch-sized.

Original asset/package hashes (36), metadata/GUID preservation and whitespace checks
passed. No subjective listening/rig-quality or physical-device acceptance is implied.
Manual check: let a reply finish, click Replay, compare mouth timing, and press Stop.
Submitting a new prompt or leaving Play clears Replay; this is session-only storage.

Reproduce in Play with **Companion > Run Reply Replay Checks**.

## Follow-up verification — 2026-10-04

The conversation-context regression failed after two passing checks: the local model
returned an unusable reply and the test timed out. The failed artifact is retained in
`context-first-attempt.txt`. Replay's 11 checks passed, but this regression is not green.

An Editor rerun is blocked by MCP routing to another open project. A path probe returned
`D:/Unity/Ramayanam/Assets`; a guarded follow-up skipped execution there. Current
AI Companion scene/layout state could not be reverified. No layout/docking/Simulator
settings were changed. Reconnect MCP specifically to `D:/Unity/ai_companion/apps/unity`,
then run **Companion > Run Conversation Context Checks** in TalkingCompanion Play mode.
Investigate recurring invalid model output before recording a pass.

Repository preservation rerun passed: 36 original asset/package hashes, metadata and
unique GUIDs, baseline secret-pattern/ignore checks (not a comprehensive security audit).

## Recovery completed — 2026-10-04

Project-pinned `unity_companion` MCP verified `Application.dataPath` as the intended
`D:/Unity/ai_companion/apps/unity/Assets`. Ollama initially was unavailable (retained
`context-service-unavailable.txt`); started the installed engine locally. Unusable
model output recurred after startup, so the adapter now requests a required text/emotion
object schema instead of generic JSON, retaining validation and failure handling.
This is a structural reliability improvement, not proof that all future output is valid.

Fresh actual Editor runs passed **7 conversation-context checks** (artifact in sibling
`conversation-context/editor-checks.txt`) and **11 Replay checks** (`editor-checks.txt`).
Node syntax, repository preservation and whitespace checks passed. Post-run error/exception
query returned no entries. Editor restored to stopped state, clean TalkingCompanion scene.
Simulator remained 1170×2532. All seven persistent window rectangles match the pre-run
snapshot; the raw all-window equality in `editor-state.txt` is false only because the
temporary ConnectionApprovalDialog disappeared. No docking/sizing/preview helpers used.
Physical audio quality, real microphone accuracy, Hindi and mobile checks remain unverified.
