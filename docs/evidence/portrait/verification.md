# Portrait M0 correction — 2026-09-26 IST

Owner requested portrait mode and implementation through M0 again. No new product approvals
were inferred. Existing Unity 6000.5.9f1 / URP 17.5.0 / Input System 1.20.0 retained.
Scene remains Assets/Companion/Scenes/MockCompanion.unity with its original GUID.

## Passed

- Live MCP: PlayerSettings reports Portrait; portrait only autorotation mask, desktop preview
  defaults 390×844; MockPanel reference resolution 390×844 and width match 0.
- Existing M0 behavior harness: 11 passes at 390×844 in the live Editor. Loading, streaming,
  completion, loading/partial cancellation, before/after-output errors, retry, replay,
  clear and character checks. See ../unity-smoke.txt and ../m0-complete.png.
- New portrait harness: 61 geometric/runtime assertions, zero failures; see layout-checks.txt.
  Phases 0/1/2: 360×640, 390×844, 1080×1920. Phase 3: 44-unit top/34-unit bottom safe area.
  Phase 4: safe area plus 260-unit keyboard exclusion with a compact visible character.
  Phase 5: large chat text, 4,500-character user message and multiline composer. All primary
  controls remained inside viewport/simulated safe region, chat retained scrolling space,
  and responses completed. Small-phone demo overlay bounds also passed.
- Visually inspected layout-0.png, layout-2.png, layout-4.png, layout-5.png, demo-controls.png
  and the final 390×844 completion capture. Keyboard exclusion is blank simulated space,
  not a real captured native keyboard.
- Re-ran seven .NET domain groups, generated drift, schema/format/negative/wire checks,
  supplemental schemas, OpenAPI references, strict TypeScript/client consumer and repository
  checks. All passed. No domain/backend/provider behavior was changed.
- All 36 original asset/package hashes still match. All 48 prior M0 metadata, scene and
  package files listed in source-snapshot.json also match. Intentional edits: presentation,
  scene-creation panel defaults, PlayerSettings orientation/preview and existing MockPanel
  scaling (same GUID). New Editor-only preview/layout-check helpers added with metadata.

## Observations and limitations

MCP discovery was intermittently unavailable during assembly reload, then recovered.
Saving assets surfaced a Unity warning about a cached immutable URP debugging PanelSettings
asset. No package-manifest/lockfile changes were made; all source package hashes match.
The project's own panel was updated in place. No user package cache was deleted or reset.

Current API execution remains blocked by the previously reported Windows Application
Control restriction; this UI-only revision did not rebuild or bypass the blocked service.
Clean Unity import, physical Android/iOS orientation/keyboard/safe-area behavior,
Hindi IME/shaping, screen readers, 200% text and device builds remain unexecuted M1/M5
verification. Editor insets are a test simulation, not device approval.

Reproduce: open the mock scene, select Companion > Portrait Preview (390 x 844), Play,
run Companion > Run M0 Play Mode Checks, then (after completion) Run Portrait Layout Checks.
Restart Play Mode to reset injected insets/content. No hosted CI, spend, signing or release.
