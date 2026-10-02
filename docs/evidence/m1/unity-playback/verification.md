# Unity playback observation — 2026-09-28

Passed: 125 CC lab Play Mode checks in the existing portrait Simulator, Unity 6000.5.9f1.
Five new assertions verify real sample cursor capture before Stop, old epoch/format,
cursor reset, duplicate-stop stability and conservative progress at source end.
Evidence: editor-checks.txt, playback-report.json and portrait-report.png.

Passed: `npm run check` (TypeScript, generated client and 95 backend voice assertions).
Passed: `node tests/e2e/check-unity-playback.mjs`, consuming the actual Editor report:
8,192 observed samples at 24,000 Hz, 90,000-sample clip, 341 ms rounded down. Synthetic
text timing keeps only a fully observed 250 ms segment and excludes the remaining tail.
This test associates synthetic labels with a tone; it is not a spoken transcript.

Report UI is within the existing scroll area; fixed reset remains visible. Scene and
source assets/GUIDs preserved. Original Editor returned to stopped Play Mode; clean CC
scene, StandaloneWindows64 target, portrait settings, same window positions/sizes and
Simulator selection as this turn's baseline. The user's current Console dock was retained.

Unexecuted: Android rebuild and physical playback/latency. Latest 171.8 MB APK predates
this runtime change. No real voice, microphone, transport network connection or provider.
The sample cursor does not measure output-device latency or human hearing. Source end
retains the last observed cursor, not a manufactured full-clip completion. Backend bridge
validates structure/expected identity only; actual authentication, delivered-media bounds,
real alignment and durable history remain necessary. M1 remains partial.

Reproduce: open CCCharacterTest, Play, run Companion > Run CC Character Play Mode Checks,
then run the Node file check from repository root. It reads the newly exported report.
