# Isolated Android diagnostic build — 2026-09-26

Status: **failed**, no APK produced; packaged-manifest verification **unexecuted**.
Command: tools/build-android-test.ps1. Unity 6000.5.9f1, Android ARM64 IL2CPP,
Development, portrait, CCCharacterTest only, local debug signing.
Run: artifacts/android-test/20260926-133019 (ignored local artifact directory).

Unity reported Failed, 1 error, 1038 warnings, duration 00:07:54.3825634.
The native linker reported `LLVM ERROR: IO failure on output stream: No space left on
device`, then `clang++: error: linker command failed with exit code 1`.
SDK repository-list download failures also appear earlier; the immediate fatal error
was native linking. See the local unity-build.log and output/build-result.txt.

The source snapshot completed fresh import and reached native linking. This does not
prove a runnable player or complete M0 fresh-checkout reproduction. After failure,
C: and D: each reported approximately 100 GiB available; the failing output volume and
peak temporary use were not established. Do not infer that a retry will succeed.

Build runner now checks at least 20 GiB available on workspace and temporary volumes
before copying files, and restores its previous output environment value. The threshold
is a conservative preflight, not a measured peak-space guarantee. No files were deleted,
no rebuild attempted, no device installation or release performed. Owner requested
Editor-only Simulator testing; build/device work stays deferred. Before a future retry,
investigate linker/temp-volume peak usage and retain the original logs.

Post-change checks: PowerShell parser passed for build-android-test.ps1 and
verify-android-apk.ps1. The new preflight has not been exercised by another build;
packaged-manifest/architecture assertions remain unexecuted. Unity MCP subsequently
confirmed the original apps/unity Editor, Play Mode stopped, StandaloneWindows64 target
and Portrait PlayerSettings. Repository preservation check passed (36 original hashes,
metadata/GUID uniqueness and baseline secret-pattern/ignore heuristic).
