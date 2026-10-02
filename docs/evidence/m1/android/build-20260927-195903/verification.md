# Current CC diagnostic Android build — 2026-09-27

**Passed:** Unity 6000.5.9f1 Android ARM64 IL2CPP development build; 0 errors,
998 warnings; build duration 00:05:59.8088837 excluding fresh import. Includes the
partial-blink correctives and session-only jaw comparison controls. Explicit scene:
Assets/Companion/Scenes/CCCharacterTest.unity. This is a local debug-signed test build.

APK: `artifacts/android-test/20260927-195903/output/Companion-CC-Test.apk`.
SHA256: `384A60331266B3E428A64FF26DA0466BF625099D5D44576E500497C6FB76054A`.
Actual APK: **171,767,334 bytes (171.8 MB)**, within the 200 MB diagnostic threshold.
Previous reduced APK: 169,373,218 bytes; increase 2,394,116 bytes (1.41%). Entry-level
comparison locates the increase in data.unity3d; this does not isolate one asset's cause.
BuildReport's 1,780,171,551 total bytes must not be presented as the APK file size.

## Evidence

- `build-result.txt`: successful build summary; raw log remains in ignored build folder.
- `apk-verification.txt`: packaged portrait orientation, debuggable flag,
  com.local.aicompanion.cctest identity, ARM64 libil2cpp.so and no other native architectures.
- `source-snapshot.json`: all 272 source input files matched, including metadata.
  Checked during fresh import before the build entry point applied isolated Android
  settings. Future build-runner calls perform the check before launching Unity.
- `mesh-subset.txt`: eight processed renderers; the policy includes 32 required native
  names, including both blink correctives. Existing 135-frame equality evidence remains
  in the blink-correction folder. No scene/source asset change was needed for this build.
- `package-comparison.json` and `entry-size-deltas.json`: direct ZIP/file measurements.
- `editor-state.txt`: original project remained stopped, scene clean, portrait,
  StandaloneWindows64; all recorded window positions/sizes match before the build.
- `python tools/check-repository.py`: 36 original file hashes, GUID/metadata and baseline
  secret-pattern/ignore checks passed. Not a comprehensive security audit.
- Snapshot-verifier fixtures accepted matching files, rejected changed/extra files,
  and blocked overwriting inputs or existing reports. Fixture: artifacts/snapshot-check-1biew9th.

## Failure recovered and warnings

Attempt 20260927-195836 **failed** before compilation when Package Manager's local IPC
stream disconnected and package resolution was cancelled. Targeted lines are retained
in `first-attempt-failure.txt`. A fresh isolated retry succeeded without changing
packages, licensing, security settings or the interactive Editor. Root cause unconfirmed.

`warning-triage.json` records shader/compiler message occurrences, which differ from
BuildReport totals because messages repeat across stages/targets. Counts match the prior
build: 978 shader-warning lines (920 unsupported inference shader variant messages,
36 signed/unsigned, 14 modulus, 5 divides, 3 loop-variable scope), plus 96 serializer
analyzer occurrences representing 16 unique lines. No warning suppression/package upgrade.
Generated DTO dictionary/object/nullable fields cannot be assumed to round-trip through
Unity serialization; establish and test the wire serializer before connecting network DTOs.
Review inference shader reachability before enabling on-device inference. Build success
does not establish that these packages are production-compatible.

## Unexecuted / blocked

No installation, physical launch, audio routing, thermal/performance or keyboard test;
owner continues to request Simulator-only tests. No iOS build/device evidence.
The prior 120 Editor checks apply to this unchanged runtime revision, not APK execution.
Full CC bone conversion, real speech/lip-sync and Q-006 provider approvals remain open.
M1 remains partial. Next safe work is verified CC reference-pose calibration and DTO
wire-serialization checks; device/provider gates remain explicit in QUESTIONS.md.
