# Android development build verification — 2026-09-26

**Build passed; package inspection passed. Device execution unexecuted/owner-deferred.**

Owner authorized a retry after freeing space. Command: tools/build-android-test.ps1.
Fresh source snapshot: artifacts/android-test/20260926-184350/project.
Unity 6000.5.9f1, Android ARM64 IL2CPP, Development, local debug signing, only
Assets/Companion/Scenes/CCCharacterTest.unity. No account, release or installation.

APK: artifacts/android-test/20260926-184350/output/Companion-CC-Test.apk.
SHA256: 29BEFA4FCCE406E347A982C96F9C27D76AB86AA19B3915475A63E62BFE7A0E94.
Size: 563,534,978 bytes (563.5 decimal MB / approximately 537.4 MiB).
Unity summary: Succeeded, 0 errors, 998 warnings, 00:06:13.4593701 build duration
(does not include initial import). See build-result.txt. Its total bytes include other
build output; use the APK file size above for the package, not Unity's totalSize.

After successful Unity exit code 0, the PowerShell wrapper remained waiting for Gradle
descendants. Only that completed-build wrapper was stopped after verifying its PID and
command. No Unity Editor or Gradle daemon was killed. The runner now waits for the Unity
process directly. This wait change passed syntax validation; a second full build was not
run solely to exercise it.

The original package checker expected hexadecimal aapt2 values; installed aapt2 renders
orientation as 1 and debuggable as true. Updated parsing and reran the checker on this
actual APK: portrait, debug flag, diagnostic ID com.local.aicompanion.cctest and ARM64
libil2cpp.so passed. No non-ARM64 native libraries. See apk-verification.txt. The first
checker failure was a parser mismatch, not a landscape manifest.

The package exceeds the requirements' 200 MB base-install target. package-size.txt shows
data.unity3d accounts for roughly 500.8 MB of packaged bytes. That archive must be broken
down with a Unity asset build report before choosing texture/mesh reductions; zip size
alone cannot attribute it to individual assets. The source character still has 252,192
triangles and 25 material slots. No production optimization claim. Existing package
versions were preserved; warnings are not a clean compatibility audit. Full local log:
artifacts/android-test/20260926-184350/unity-build.log.

Runtime code matches the snapshot. The new CCDeformationAudit is Editor-only and was
added after snapshot creation; it is intentionally not a player feature. The original
apps/unity project, scene, build target and Editor layout were preserved. Source hashes
and GUID checks passed; see the calibration evidence linked below.

Unverified: launch on Android, actual rotation lock, cutouts/keyboard, native audio routes,
thermal/memory/FPS, acoustic lip sync, iOS build/device, production build guard rejection.
The earlier Simulator checks remain Editor evidence. M1 is partial, and the optional API
Application Control blocker is unrelated and unchanged.

[Source/layout preservation evidence](../../cc-calibration/verification.md).
