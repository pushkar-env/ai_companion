# Runtime history navigation and portrait layout

2026-10-05, Windows Unity 6000.5.9f1. Project-specific MCP verified apps/unity.

Passed: 13 checks in [checks.txt](checks.txt) from Companion/Run History Navigation Layout
Checks in Play mode. Temporary additive scene, cloned PanelSettings and RenderTexture;
no Game-view size/selection or Editor window operations. Viewports 360x640 and 390x844,
24px simulated top/bottom insets. Checks cover resolved layout, all three action bounds
and minimum 44px height, hidden underlying content, Back restoring original elements,
exactly one interrupt per open, and cleanup leaving only original content.

[360x640](360x640.png) and [390x844](390x844.png) were viewed: disclosure, guidance, Back
and disabled setup-state actions are readable, wrapped and within bounds. These are actual
runtime UI Toolkit renders, not mockups. No populated history, Hindi font shaping or
large-text accessibility approval is implied by these captures.

An initial stopped-Editor layout attempt failed because the runtime panel had no resolved
bounds. Test now explicitly requires Play. After passing, Play was stopped again; active
TalkingCompanion scene clean, unchanged path and original five root objects. Error Console
empty before test. No GUID rewrite, orientation change or scene asset save.

Unexecuted: populated runtime conversation through this new route, pointer/keyboard
navigation in the actual Game view, physical safe areas/lifecycle, standalone development
fixture delivery, IL2CPP and production auth. Previous actual API/view checks remain
separate evidence. Backend suite not rerun because no backend files changed this turn.
