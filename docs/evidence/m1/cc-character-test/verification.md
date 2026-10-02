# CC character test verification

Latest-run update (2026-09-27): checks.txt now contains 108 passing assertions after the
partial-blink correction; see ../blink-correction/verification.md for preserved checks and
comparison captures. The 98-check result below describes the initial implementation.

2026-09-26, Windows Editor, Unity 6000.5.9f1, existing URP; no Git commit/build ID yet.
Scene: Assets/Companion/Scenes/CCCharacterTest.unity.

Passed 98 assertions via Companion > Run CC Character Play Mode Checks in Play Mode.
See checks.txt. Each of 29 native controls resolves, applies and resets; additional
checks cover shared-mesh blink, jaw return, invalid channel/NaN, 390x844 preview/reset
reachability, sample-clock facial motion, interrupt epoch/reset, completion, background
callback simulation and 20 play/stop cycles. Existing core/API were not changed; the
unrelated API Application Control block was not retested for this scene-only change.

neutral.png, blink.png, open.png are actual Play Mode captures, visually reviewed.
This folder is the latest-run output and was subsequently refreshed by the iOS notch
Simulator run (1170×2532); the 390×844 checks above describe the initial run. Separate
Android and iOS profile captures/checks are retained in ../simulator/; see its
verification.md for dimensions, safe areas and scope.
Basic URP diffuse/normal materials and two lights are diagnostic appearance, not final
skin/hair/eye shading. Body is held in its imported pose; no idle/body animation yet.
The jaw assist opens the mouth visibly but remains approximate. No real speech/capture,
52-channel calibration, physical Android/iOS result, native interruption proof or
acoustic latency claim. Scene non-development build rejection is implemented but a
non-development player build was not executed. License/production gates remain open.

Original 121 model files verified unchanged against cc5/source-hashes.json.
Existing scene assets and packages were not edited; additional materials use the
existing extracted textures with normal-map import flags adjusted on that copied set.
