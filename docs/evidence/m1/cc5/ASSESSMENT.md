# CC5 character inspection — 2026-09-26

Verdict: technically suitable for a local real-character facial prototype, with an
adapter and material setup. Not approved for mobile shipping or full FACE-01/02 acceptance.

Source: owner-exported `models/femaleCC.Fbx`, `femaleCC.json`, texture folders. JSON
reports `RL_CC3_Plus` and ExpressionSet 6. CC5 export can contain a CC3+ character;
this label is not an import failure. All 121 source files are unchanged against
`source-hashes.json`. No license/receipt/readme was present; content-pack name and
trial/licensed installation status requested under Q-010. No rights inferred.

## Measured findings

| Item | Result |
|---|---|
| Existing Editor | Unity 6000.5.9f1, existing project and URP retained |
| Isolated import | `Assets/Companion/Imported/CC5Inspection/femaleCC.Fbx`; source preserved |
| Skeleton | Initial Generic import changed to Humanoid on inspection copy; Avatar.isValid and isHuman both true |
| Meshes | 13 skinned renderers; 25 material slots (not a measured draw-call count) |
| Geometry | 252,192 triangles across imported skinned meshes; body alone 111,888; hair 79,212 |
| Facial content | Body 348 blendshapes, brows 395; additional shapes on tongue, teeth, tearline and occlusion meshes |
| Texture references | All 117 distinct JSON texture paths resolved through source folders or embedded extraction; 37 embedded images extracted |
| Source size | 321.9 MiB combined; FBX 148.6 MiB. This is not player build size or runtime memory |
| Deformation probes | Eight named body channels baked independently at weight 100; every probe changed vertices; see deformation-checks.txt |
| Visual evidence | neutral.png, blink.png, pucker.png; simplified temporary URP Unlit materials, not final appearance |

Raw inventory in `unity-import.txt` records the original Generic import. The later
`textures-rig.txt` records successful Humanoid conversion. Unity reported an animation
import warning after conversion; imported animation clips/retargeting remain unverified.
The Editor restarted during inspection (AndroidPlayer module is now present); preview
checks subsequently completed in the reopened Editor. No Android build/device pass claimed.

## Required integration work

1. Map canonical channels by name and across all affected meshes. Examples:
   eyeBlinkLeft → Eye_Blink_L, mouthSmileLeft → Mouth_Corner_Pull_L. Some mappings combine
   multiple CC shapes; all 52 require calibration, side validation and conflict handling.
   The existing strict synthetic validator cannot accept CC names directly.
2. Account for expression bone data. JSON includes jaw/eye/teeth/tongue bone transforms
   for several channels. Jaw_Open and V_Open have actual mesh deltas, but that does not
   prove a complete jaw/teeth movement from blendshapes alone. Implement/test the bone
   contribution or evaluate a morph-converted export. Do not discard that metadata.
3. Build persistent URP materials with normals, skin, hair alpha and eye settings.
   Inspection used diffuse-only unlit materials; normal map/alpha import defaults and
   production lighting were not tuned. No Reallusion Auto Setup plugin installed.
4. Optimize a derivative, preserving this source: lower geometry/LODs, reduce hair and
   teeth complexity, consolidate material slots/textures, and strip unused morphs only
   after calibration. Current geometry exceeds the spec's 70k LOD0 target (~3.6x);
   25 material slots also exceed its 12 skinned-draw target before any additional passes.
5. Benchmark on the S23. English/Hindi speech timing, real audio, memory/thermal/FPS,
   clothes/gesture clipping and all 52 channel combinations remain unexecuted.

## Reproduction and preservation

`tools/unity-inspect-cc5.cs` is a Unity MCP RunCommand script, not a runtime component.
It creates a temporary preview scene, bakes eight facial probes and three posed images,
then closes that scene and disposes temporary objects. Mesh baking is used for static
captures to avoid skinning update latency during synchronous Editor commands.
Existing app scenes, their GUIDs, package versions and pipeline were not changed.
No production avatar replacement, publication, purchase or external upload occurred.
