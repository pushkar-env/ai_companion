# Avatar contract, animation and facial standard

## AVATAR-01 — asset contract

Each avatar declares `avatar_id`, immutable `version`, `rig_family`, `rig_version`, `facial_profile`, `body_mask_schema`, skeleton path/bone bindings, material slots, LODs, sockets, bounds, license reference and approved wardrobe compatibility. Use a documented humanoid skeleton convention, centimeters/meters conversion at import, bind-pose tests and animation retarget validation. Runtime cannot trust arbitrary uploaded meshes; custom user avatars are P2.

Import rejects missing bones, inverted normals, broken weights, unsupported shaders, excessive geometry/materials, out-of-range shape values or absent required facial bindings. Do not assume all avatar families share one mesh topology. Wardrobe follows rig/body compatibility metadata and visual approval rather than string-name guessing.

## FACE-01 — project canonical 52-channel profile

Adopt the following ARKit-style names as a project interchange convention. They are not a guarantee that every asset/provider supplies them. No camera, face capture, TrueDepth device or Apple-only runtime is needed to drive them from speech/emotion on Android or iOS. Values are normalized 0..1 in contracts, converted to Unity 0..100 weights at the mesh adapter. Left/right refer to the avatar's anatomical sides.

```text
browDownLeft browDownRight browInnerUp browOuterUpLeft browOuterUpRight
cheekPuff cheekSquintLeft cheekSquintRight
eyeBlinkLeft eyeBlinkRight eyeLookDownLeft eyeLookDownRight
eyeLookInLeft eyeLookInRight eyeLookOutLeft eyeLookOutRight
eyeLookUpLeft eyeLookUpRight eyeSquintLeft eyeSquintRight
eyeWideLeft eyeWideRight
jawForward jawLeft jawOpen jawRight
mouthClose mouthDimpleLeft mouthDimpleRight mouthFrownLeft mouthFrownRight
mouthFunnel mouthLeft mouthLowerDownLeft mouthLowerDownRight
mouthPressLeft mouthPressRight mouthPucker mouthRight
mouthRollLower mouthRollUpper mouthShrugLower mouthShrugUpper
mouthSmileLeft mouthSmileRight mouthStretchLeft mouthStretchRight
mouthUpperUpLeft mouthUpperUpRight
noseSneerLeft noseSneerRight tongueOut
```

Full production profile includes 52 bindings, with explicit approved zero/no-op bindings only where geometry cannot meaningfully support a channel (such as tongue). Reduced mobile profile may derive/combine channels in a versioned calibration table and must pass the same perceptual speech/blink tests. Missing mappings must be reported, not silently ignored. Per-avatar calibration stores gains, clamps, neutral offsets and conflicting-channel rules.

## FACE-02 — lip sync pipeline

Canonical viseme set: `sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou`. A versioned matrix maps each viseme to facial channels for each rig. Prefer timestamped provider phonemes/visemes when actually supported; otherwise use a licensed, validated local/server speech-to-viseme analyzer on the exact output audio. Audio-amplitude jaw motion is a labeled prototype/degraded fallback, not production speech quality. Do not promise phoneme timing from a provider that supplies only audio.

Drive facial frames from audible playout sample time (DSP clock plus measured output latency), not packet arrival or transcript time. Include utterance ID, playback epoch, start offset and duration. Support jitter buffering, interpolation, coarticulation, attack/release, pauses and silence. Flush visemes and stop mouth motion on interruption/cancellation/route reset; discard late frames for prior epochs. Buffer video expression changes to align with speech if needed, without unbounded latency.

## AVATAR-02 — layered behavior

Animation priority: safety/explicit mute and session end → speech mouth/jaw → blink/eye gaze → emotional upper face → idle/body gestures. Use per-channel masks; avoid two controllers overwriting the mouth. Emotion envelopes are bounded and smoothly blended. Gaze avoids constant staring, gestures avoid clipping and idle motion respects reduced-motion settings. Emotion values represent character expression, not inference about a user's mental health.

Acceptance: import validator exercises all 52 names, duplicates and ranges; diagnostic scene independently drives every channel. Human review uses diverse phoneme recordings in every launch language and every shipping rig. On reference devices, p95 audio/visual offset is ≤80 ms, no offset exceeds 150 ms in a 10-minute test, blink/eye direction are correct and mouth returns to rest within 200 ms after audible speech stops. Every clothing/body/gesture combination in the release compatibility set has artifact-free approved captures.
