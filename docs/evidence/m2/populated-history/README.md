# Populated runtime history and larger messages

2026-10-05, Windows Unity 6000.5.9f1, PostgreSQL 18.1, .NET 10.

Passed:

- `python tools/check-database.py --api --unity-runtime`: 114 groups including runtime bridge.
- **Companion > Run Populated History Layout Checks**, in Play: 16 checks in [checks.txt](checks.txt).
- Six real-database synthetic completed turns hydrate through runtime navigation/SSE;
  normal and 150% body text retain a scrollable viewport, all fixed actions fit simulated
  safe areas with ≥44px heights, final reply is reachable, pause retains history and Back
  restores original chat elements.
- Captures visually inspected: [normal](normal-360x640.png), [larger messages](large-360x640.png),
  [last reply](large-bottom-360x640.png). Actual UI Toolkit renders at 360x640 with 24px top/
  bottom insets. Text is clipped only by the intended scroll viewport; controls stay visible.

Final cluster artifacts/database-tests/run-ywigcnln, stopped. Ignored credential fixture
removed. No provider calls, real data or credentials in captures. Runtime scene and
RenderTexture cleaned up; Play restored to stopped. TalkingCompanion still clean with
original five roots; Console error check empty. No Game-view/layout/package changes.

Unexecuted: physical touch/keyboard interaction, native phone safe areas, OS-wide font
scaling, screen readers, Hindi font shaping, large-history performance, and end-to-end
runtime token expiry/network loss. The larger-message toggle is session-only and affects
message body text, not every UI label or system accessibility setting. Production auth,
provider and real-user retention gates remain unchanged.
