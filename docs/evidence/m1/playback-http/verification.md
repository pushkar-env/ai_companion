# Synthetic playback HTTP verification — 2026-09-29

Environment: Windows, Node 24.12.0, existing TypeScript 5.9.3. No added dependencies.

- Passed: `npm run check` — TypeScript/generated-client checks, 114 offline voice
  assertions and 19 actual loopback HTTP assertions. See `checks.txt`.
- Passed: `node tests/e2e/check-unity-playback.mjs` — saved Editor report retained
  341 ms conservatively and excluded the unplayed synthetic tail (offline import).
- Passed: `python tools/check-repository.py` — 36 pre-existing asset/package hashes,
  metadata presence, unique GUIDs and baseline secret-pattern/ignore checks. The latter
  are not a comprehensive security audit.
- Passed: HTTP account isolation, exact Host and browser-Origin rejection, bad/expired
  tokens, body/format bounds, server-controlled delivery limits, conservative receipt,
  identical retries, conflicting retries, per-token budget isolation and credential cleanup.
- Initial failed check: fetch rewrote the custom Host header, so the foreign-Host test
  did not exercise the intended request; the failed process also emitted a Windows
  libuv shutdown assertion. Replaced the test client with native HTTP requests that
  fully consume responses and disable connection pooling. The complete suite then exited 0.
- Unity Editor/Simulator checks and Android rebuild: unexecuted this turn; no Unity
  files changed. Latest APK still predates the Unity playback-report implementation.
- Blocked: live provider integration pending Q-006 setup decisions; physical device
  acceptance remains outside the current Editor-only scope. The .NET API's existing
  Windows Application Control block remains unresolved.

The service uses only synthetic identities, delivery manifests and transcript timings.
Tokens are ephemeral and never written to logs. Reports establish bounded client claims,
not acoustic hearing. No TLS, production identity, durable state, microphone, native
transport, provider media or actual Unity-to-HTTP integration is demonstrated.

Reproduce from repository root: `npm run check:voice:http`. The runner creates an
ephemeral localhost service and closes it automatically; no manual credentials needed.
