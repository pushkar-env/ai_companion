# Reduced CC diagnostic Android build

Built 2026-09-26; result reconciled and package inspection rerun 2026-09-27.
Unity 6000.5.9f1, Android ARM64 IL2CPP Development, local debug signing. Explicit scene:
Assets/Companion/Scenes/CCCharacterTest.unity. Isolated source snapshot under
artifacts/android-test/20260926-192707/project; original apps/unity remains in place.

**Passed:** Unity build, 0 errors, 998 warnings, build duration 00:05:56.1066330 (excludes
fresh import). Package inspection: portrait orientation, debug flag, diagnostic ID
com.local.aicompanion.cctest, ARM64 libil2cpp.so, no other native architectures.
The build runner saved verification automatically after Unity exited; the prior Gradle
descendant-wait issue did not block this run. The old tool session expired before
resumption; evidence is the saved report, exit-0 log and reverified APK.

APK: artifacts/android-test/20260926-192707/output/Companion-CC-Test.apk.
SHA256: 25A0DC03170EB10BA105EC77368396B1D53B609253723EA77621E4E25F273D3D.

| Measurement | Full rig baseline | Reduced diagnostic |
|---|---:|---:|
| Actual APK bytes | 563,534,978 | 169,373,218 |
| APK decimal MB | 563.5 | 169.4 |
| Unity uncompressed meshes (rounded label) | 842.9 mb | 5.1 mb |
| Unity uncompressed textures (rounded label) | 75.8 mb | 75.8 mb |
| Unity total user assets (rounded label) | 949.8 mb | 112.0 mb |

Saved **394,161,760 bytes (69.94%)**. The diagnostic APK meets the 200,000,000-byte
comparison threshold. This does not certify full production install size: additional
features/content and complete production facial coverage remain. See the
[machine-readable comparison](../../mesh-subset/package-comparison.json) and
[geometry preservation checks](../../mesh-subset/verification.md). mesh-subset.txt
confirms processing of the intended eight mesh renderers.

No texture downsampling, triangle reduction, material change or package removal. The
source/imported rig and Editor scene stay complete. Physical launch, rotation, keyboard,
native audio routing, memory, GPU/thermal performance and iOS build remain unexecuted.
No device was installed or release published. Warnings remain for a future compatibility
audit; success is not a warning-free build. M1 remains partial.
