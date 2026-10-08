# History rendering and keyboard evidence

2026-10-05, Windows Unity 6000.5.9f1. Synthetic local data only.

Before changing the view, recorded three warm detached renders of 1000 completed turns:
125.257 / 113.140 / 71.467ms in [before.txt](before.txt). With retained cards:
0.159 / 0.146 / 0.127ms in [after.txt](after.txt). Same fixture, Editor and measurement
boundary (Stopwatch around Render after warming). This measures unchanged UI construction,
not frame time, initial population, layout/repaint, RSS or a mobile target. The allocation
counter returned zero both times and is treated as unverified, not zero-allocation evidence.

Passed:

- **Companion > Run History Scale and Keyboard Checks** in Play: 13 [checks](checks.txt).
  1000 turns, unchanged row reuse, terminal update in existing card, stable scroll offset,
  larger text without row replacement, keyboard Home/End/Page Up/Page Down, focus/outline,
  over-cap rejection preserving cursor and account reconfiguration clearing retained rows.
- [Keyboard focus capture](keyboard-focus.png) visually inspected: final message visible,
  cyan outline clear, controls readable. Synthetic events were injected into the projection
  for this scale test; no network/AI request was required.
- Twelve runtime HTTP recovery checks rerun, preserving retry/auth behavior.
- Eight actual PostgreSQL/API Editor checks rerun through full
  `python tools/check-database.py --api --unity`: 114 groups passed. Cluster
  artifacts/database-tests/run-7hixomp9 stopped; ignored credentials removed.
- Scoped whitespace check passed; Console errors empty. Play restored to stopped,
  TalkingCompanion clean with original five roots; no Game-view/layout/scene asset changes.

Limitations: initial display still creates all rows and snapshots still copy the retained
turn list; virtualization is pending. Keyboard tests dispatch actual UI Toolkit keyboard
events programmatically, not physical keyboard/TalkBack/VoiceOver input. No OS font scaling,
Hindi shaping, device thermal/memory/soak or screen-reader readiness claim. Existing
production identity/provider/retention gates unchanged.
