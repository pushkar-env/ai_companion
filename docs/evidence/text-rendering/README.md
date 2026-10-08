# Text rendering verification — 2026-10-07/08

Unity 6000.5.9f1, local portrait Editor. User-reported fragmented chat glyphs.

- `before-native.png` and `no-outline-native.png`: same 1170x2532 panel and conversation, changing only text outlines. `comparison.png` shows both reduced to 390x844. Removing outlines restores solid letter shapes.
- `fixed-native.png`: saved stylesheet, 1170x2532 target with 390x844 logical layout; resolved message outline = 0.
- `fixed-native-scaled.png`: that native capture reduced with Lanczos for inspection.
- `fixed-390.png`: independent actual 390x844 render of the saved stylesheet.

Final captures visually reviewed: greeting, metadata, quick replies, status and composer text render cleanly. Fresh Play session has a different transcript from the diagnostic A/B; compare the original A/B for causal evidence. No font or panel asset replacement. Drawer text now uses a soft shadow, preserving transparent backgrounds.

The screen uses UI Toolkit/TextCore with SDFAA font assets. The outline effect caused the defect; Simulator scaling made it more apparent. This does not establish a general Unity/TMP defect.

Earlier temporary scheduled capture callbacks referenced a destroyed RenderTexture and produced QA-only exceptions. On Oct 8 resumption Editor was stopped with the original panel and no target. Final captures used explicit setup/capture/restore calls, with no scheduled capture callbacks. Restored stopped Play state and original MockPanel. No Editor layout, Game-view selection or build-target changes. Physical-device testing remains pending.
