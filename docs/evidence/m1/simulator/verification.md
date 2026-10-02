# Device Simulator verification — 2026-09-26

Unity 6000.5.9f1, existing apps/unity project and URP. CCCharacterTest scene.
Owner requested Editor-only testing. These are simulated screens, not phone builds.

| Profile | Screen / safe area (Unity coordinates) | Result |
|---|---|---|
| Punch Hole Center | 1440×3088 / (0,0,1440,2999), Portrait | 98 assertions passed |
| iOS Notch Device | 1170×2532 / (0,102,1170,2289), Portrait | 98 assertions passed |

Per-profile checks.txt and neutral/blink/open PNGs are saved in android-punchhole/ and
ios-notch/. Checks cover 29 native bindings, application/reset, multi-mesh blink, jaw
reset, invalid input, preview/reset reachability, sample-clock playback, interruption,
completion, simulated background callback and 20 sessions. The Android profile also
received manual Simulator clicks on Blink and Interrupt/reset; the visible pose changed
and returned to neutral. Both profile layouts were visually inspected.

The existing Game pane was changed to Simulator using its dropdown. No window was
moved, resized or docked. Comparing ../android/editor-layout-before.txt with
../android/editor-layout-simulator.txt, treating SimulatorWindow as the same pane as
GameView, gives identical window rectangles. ../android/ios-simulator.txt records the
second profile metrics. The main build target remained StandaloneWindows64.

Unexecuted: physical rotation lock, Android/iOS player behavior, native keyboard and
Hindi input, microphone/route/phone-call interruption, GPU/thermal/memory benchmarks,
real voice and calibrated lip sync. An iOS screen profile is not iOS device evidence.
The simulator tone and mouth cues are synthetic. M1 remains partial.
