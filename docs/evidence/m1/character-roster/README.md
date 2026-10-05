# Character roster verification - 2026-10-04

Environment: Windows, Unity 6000.5.9f1, existing URP project, pinned unity_companion MCP,
TalkingCompanion scene, existing portrait Simulator at 1170 x 2532. All verification used
the interactive Editor without changing layout, Game-view selection or build target.

## Passed

- `Companion.Editor.CharacterSwitchChecks.Run()`: **40 checks**, including real baked-mesh
  deformation for speech, blink, smile, inner-brow, frown and eye widening on each model.
  Header selection activates one rig, preserves draft/microphone choice, and fits portrait.
  One actual local AI response and installed Windows TTS exercised switching during
  generation and audio playback; sample position and clip are retained. Each model plays
  cached speech with jaw motion. Stop, Retry and New chat retain their previous semantics.
  See `editor-checks.txt` and the three portrait screenshots.
- `Companion.Editor.VoiceInputChecks.Run()`: **11 checks**, using generated PCM (no user's
  microphone recording). Started on Alita, switched to Cosmos while recognition was in
  flight; editable draft arrived without auto-send and cancellation worked. The copied
  current-run result is `transcription-checks.txt`.
- `node tests/e2e/check-talking-service.mjs`: **12 boundary checks**; local readiness suite:
  **8 checks**, including actual installed-model readiness. Repeated after service restart
  with no environment override, verifying the machine-local model file is used.
- `python tools/check-repository.py`: original 36 asset/package baselines, metadata presence,
  unique GUIDs, bounded secret-pattern/ignore checks passed.
- **150 existing metadata files and 227 original source files** have unchanged SHA256s.
  Seven persistent Editor window types/rectangles unchanged (`preservation-checks.txt`).
- Filtered Unity console: zero errors/exceptions. Scene saved, Play mode restored stopped,
  original character remains scene default. Portrait-only PlayerSettings retained.
- Material references valid: Alita 90,595 triangles / 24 material slots; Cosmos 91,365 /
  25 slots. Original assets and pipeline/package versions preserved.

## Corrections made during verification

New CC exports use extended expression names; mapping is required for existing emotions.
Cosmos's white procedural eye-occlusion mask initially covered its eyes. That overlay is
now transparent; its visible eyeball/cornea materials remain textured. The final screenshots
were visually inspected. Cosmos camera distance is 0.95 m; Original and Alita use 1.25 m.

The historical qwen2.5:7b was absent on this PC. qwen3:8b was already installed; selected it
in ignored artifacts/talking-character/model.txt and verified actual text/speech generation.
Local chat requests disable thinking to reserve the bounded token budget for spoken output.
No model download or external provider was used. The default fallback remains unchanged.

## Limits and unexecuted checks

This verifies the Windows Editor development scene, not Android/iOS or production readiness.
Physical microphone accuracy, acoustic AV sync, device interruption/routes, mobile GPU/RAM,
LODs and load-time budgets remain unexecuted. Three models stay resident; only one renders.
Hair/skin are prototype URP Lit conversions; Reallusion procedural shaders are not fully
ported. Cosmos eye-shadow overlay is intentionally hidden pending that art/shader work.
All appearances share the same local English Windows voice, prompt and session-only chat;
Hindi, distinct production voices, separate companion identities and a shipping avatar
catalog are not implemented by this change. M1 and M3 production/device gates stay open.

## Reproduce

Open apps/unity with Unity 6000.5.9f1, use Companion > Open Talking Companion, then Play.
Choose Original / Alita / Cosmos in the top dropdown. Type and Send, switch while speaking,
then Replay; use Record voice only when you explicitly want microphone capture. New chat
clears conversation while retaining the current appearance. Stop Play restores Original.
The local AI model must already be installed; follow docs/runbooks/TALKING_COMPANION.md.
