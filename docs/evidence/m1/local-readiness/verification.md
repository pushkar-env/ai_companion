# Local readiness — 2026-10-03

Unity 6000.5.9f1, TalkingCompanion, existing portrait Simulator. Read-only authenticated
endpoint inspects local Ollama model availability; no inference, model loading, download,
microphone recording or speech playback is performed by this check.

Passed:

- 8 Node checks (`service-checks.txt`): selected-model match, missing model, implicit
  latest tag, offline engine, malformed response, HTTP failure, authentication and actual
  installed engine/model. Failure cases use injected fetch fixtures; engine not stopped.
- Actual Editor startup reports ready. Stopped only the local Node conversation service,
  rechecked, saw unavailable feedback with draft intact; restarted service, rechecked,
  and recovered ready status with the same draft (`editor-checks.txt`).
- 12 existing service boundary checks passed.
- 19 real Editor conversation checks passed while also triggering a setup probe; speech,
  expressions, Stop/Retry and portrait bounds remain working. Touch-sized setup control
  and bottom actions fit, and screenshot `ready.png` was visually inspected.
- Repository preservation (36 original hashes, metadata/GUIDs), whitespace check and
  final Console error/exception query passed. Play stopped; clean scene and unchanged
  window rectangles recorded in `editor-state.txt`.

Recognition status reports configuration only. Installed packages, model-file integrity,
audio routing and actual voice quality are not certified by readiness. The existing
speech/transcription integration tests provide separate evidence. Physical microphone,
Hindi, mobile and missing-model UI on a changed real installation remain unexecuted.

Reproduce service checks with `node tests/e2e/check-local-readiness.mjs`. In Play, click
the setup status under the header to recheck. Start Ollama if advised, or use Companion >
Start Local Talking Service when the service is unavailable, then recheck.
