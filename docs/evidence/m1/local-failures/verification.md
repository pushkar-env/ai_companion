# Local failure recovery — 2026-10-03

Implemented bounded known-code messages and consistent NDJSON error frames for
/turn-stream, both before and after stream headers. Unknown errors never render raw
server text. HTTP failure results still consume framed reasons. Transcription recovery
is distinct from retrying a chat prompt; no automatic retries introduced.

Passed service checks (`service-checks.txt`): actual invalid-session HTTP 401, invalid
input HTTP 400 and concurrent-turn HTTP 429 all return the expected newline-terminated
error frame. Concurrent test starts real local inference and aborts its stream afterward.
The existing 12 local service boundary checks also passed.

Editor failure checks use injected error frames for busy, unauthorized, timeout, local
model unavailable, speech unavailable, invalid model reply and untrusted arbitrary code.
They exercise the real frame parser, failure handler, safe messages and silent stopped
playback. These fixtures do not prove a real 90-second timeout or failure of installed
speech tools; those outage scenarios remain unexecuted.

All 17 Editor checks passed (`editor-checks.txt`), followed by all 19 real conversation
checks in the sibling talking-companion evidence. Repository preservation and whitespace
checks passed. An initial conversation verification artifact reported Play stopped and
its screenshot write failed; the completed rerun passed all 19 checks. A fresh recovery
screenshot is captured separately instead of claiming the failed write succeeded.
`recovery.png` saved successfully and was visually inspected. Portrait controls fit;
scene remained clean, original window rectangles preserved, and Play returned to stopped
(`editor-state.txt`).

Unity's command bridge temporarily failed during imports; retry confirmed compilation
and the new check type loaded. No Unity/package/layout changes were required.

Reproduce service tests with `node tests/e2e/check-local-failures.mjs`; in Play choose
**Companion > Run Local Failure Checks** for the error-frame/UI checks.
