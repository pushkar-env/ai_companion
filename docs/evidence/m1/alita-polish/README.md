# Alita-only polish and Editor performance

Completed 2026-10-05; baseline/optimized captures began 2026-10-04, final regression
checks completed on continuation. Unity 6000.5.9f1, URP, Windows, RTX 5070, 31,801 MB
system memory; existing 1170 x 2532 portrait Simulator, uncapped frame rate/vSync off.

## Implemented

Alita is the only character in TalkingCompanion and CCCharacterTest. Original/Cosmos
imports, prior diagnostic materials and obsolete roster tools are outside Unity Assets
in models/archived-unity, with their GUIDs preserved. Original source exports remain
unchanged. Old scene backups retain their GUIDs and must not be restored alongside
active scene copies. The single-character header displays Alita without a picker.

Derived Alita runtime meshes retain exact geometry, UVs, normals, tangents, skinning and
used morph frames. Unused shapes are omitted; animation batches desired weights and
writes only changes. Stop/reset flushes neutral immediately. Mesh readout dropped from
123,572,342 to 24,344,782 bytes during construction (80.3% reduction for Alita alone).
Imported meshes are preserved for authoring and full diagnostic use.

Polish includes 1.15 m portrait framing, warm key/cool fill and adjusted ambient lighting,
softer skin normals, hair alpha coverage and smoother highlights, eye highlights,
confirmed 4x portrait render-target antialiasing, and dark readable input fields.
Hero diffuse maps retain up to 2048 px; hero normals 1024 px, supporting maps 512 px,
with compression, mipmaps and anisotropic filtering. No pipeline/package upgrade.

## Measured comparison

Same Alita portrait before/after, five-second warm-up, 30 seconds idle then 30 seconds
cached actual synthesized speech. AI generation and first-load time are outside these
steady-state samples. Speech repeats the cached answer; this is not a 20-call leak test.
Raw samples and summaries are in baseline.csv/txt and optimized.csv/txt.

| Metric | Before: three referenced rigs, Alita active | After: Alita only, optimized |
|---|---:|---:|
| Referenced mesh data | 1,083.9 MiB | 23.5 MiB |
| Referenced material textures | 595.4 MiB | 27.8 MiB |
| Idle frame p95 | 6.837 ms | 5.702 ms |
| Speech frame p95 | 4.431 ms | 4.379 ms |
| Idle character Update p95 | 0.1229 ms | 0.0436 ms |
| Speech character Update p95 | 0.1142 ms | 0.0517 ms |
| Idle frames above 100 ms | 0 / 6,213 | 0 / 8,585 |
| Speech frames above 100 ms | 1 / 9,497 | 0 / 9,572 |

Referenced resource sizes are Unity Profiler.GetRuntimeMemorySizeLong sums of unique
meshes/material textures reached from registered characters. They are NOT OS resident
memory or whole-Editor allocation: Editor imports, authoring sources, render targets,
AI inference and other allocations are excluded. Main frame times include Editor and
Simulator overhead, and the runs were not a controlled device benchmark. A scoped
Companion.CharacterUpdate profiler marker measures controller work only. One before-run
speech outlier was 144.527 ms; no cause was isolated. Do not attribute it conclusively to
an optimization. The conservative benefit is reduced retained asset data and lower
measured facial-update cost, with short-run Editor rendering remaining responsive.

## Verification

- 123 mesh checks: exact retained geometry/skinning and morph delta frames, valid materials,
  all ten speech controls, one Alita default and no archived-character scene dependencies.
- 15 face/UI checks: actual baked deformation and immediate neutral reset for mouth,
  blink, smile, brow, frown and eye widening; portrait controls fit; no stale selector.
- 11 fresh real local AI/TTS Replay checks: complete multi-sentence playback, animated
  replay, Stop, unchanged context, correct button bounds and New chat cache release.
- 11 fresh synthetic-audio transcription checks passed without recording a microphone;
  review/edit and cancellation retained. Filtered Unity console showed no errors/exceptions.
- Migrated full diagnostic mesh-subset policy passed exact retained-frame/geometry checks
  for Alita (diagnostic-subset-checks.txt).
- Repository preservation check passed: 36 initial asset/package baselines, metadata
  completeness/unique GUIDs and bounded secret-pattern/ignore checks.
- All 608 pre-change GUIDs retained in Assets or reversible archive; all 348 pre-change
  model source files unchanged. Seven Editor window types/rectangles unchanged.
- Portrait screenshot polished.png was inspected; portrait RT reports four AA samples.

## Open production criteria

**Not a mobile performance pass.** Full rig remains 90,595 triangles / 24 material slots.
Portrait frustum candidates are 83,935 triangles / 23 slots, exceeding the initial 70k
visible triangle / 12 skinned-draw targets (slots are a conservative proxy, not GPU draw
capture). Fallback <=25k LOD and material atlasing/consolidation still need authoring and
visual validation. Hair/skin are improved URP prototype materials, not a complete
Reallusion shader port or final art approval.

Android/iOS release profiling, 30-minute thermal/battery soak, OS resident/peak memory,
50 outfit switches/20 call cleanup test, cold-start distribution, GPU frame capture and
acoustic lip-sync/interruption timing remain unexecuted. Physical microphone quality is
also unverified. Current local English Windows voice and session-only storage limitations
remain; no production provider, Hindi speech or native mobile transport added.

## Reproduce

Open apps/unity in Unity 6000.5.9f1, Companion > Open Talking Companion, then Play.
Alita loads automatically. Use typed or reviewed microphone input, Stop, Retry and Replay.
Outside Play: Companion > Check Alita Runtime Meshes. In Play: Companion > Check Alita
Portrait and Face. Through pinned Unity MCP invoke AlitaPerformanceChecks.Run("label")
from Companion.Editor to collect the same workload without opening/rearranging Profiler
windows. AlitaPolishSetup is a guarded one-time migration and must not be rerun to play.
