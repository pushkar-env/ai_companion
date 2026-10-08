# Skin-tone customization

Verified in Unity Editor, completed 2026-10-07.

- `material-checks.txt`: 13 checks for matching skin, legacy saves, invalid indices,
  stable material instances, source preservation and disposal.
- `ui-checks.txt`: 24 checks using UI Toolkit navigation-submit events for swatches,
  local persistence/reload, Cancel, reset, 44px targets and compact portrait bounds.
- `comparison.png`: all six tones, face closeups and full body; authored skin details
  retained and face/body consistent. Individual images are `tone-*-face/body.png`.
- `style-skin-tone.png`: 390x844 live Style panel with Brown preview selected.
- `style-skin-compact.png`: 360x640 Style layout with Deep selected.

Screenshots use an isolated offscreen panel without changing Editor docking or Game
view selection. Tests restore local saved preferences. This is Editor evidence, not
physical Android/iOS or assistive-technology validation.
