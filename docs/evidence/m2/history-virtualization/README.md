# Virtualized synthetic history

2026-10-05, Windows, Unity 6000.5.9f1; portrait offscreen panel 360x640.

Passed: 15 checks in [checks.txt](checks.txt) from Companion > Run History Scale and
Keyboard Checks in Play mode. 1000 canonical turns remain available while at most 32
cards are bound at initial/end positions. Offscreen completion, reflow to 24px, scroll
preservation, Home/End/Page Up/Page Down, focus indication and account clearing pass.
[Focus capture](keyboard-focus.png) visually inspected; final updated reply is reachable.

Regression: 16 populated runtime checks, normal/large/bottom captures inspected; full
`python tools/check-database.py --api --unity-runtime` passed 114 groups, including that
runtime bridge (run-a3c95u1c). An earlier harness run timed out awaiting the Editor; it
was rerun successfully. An initial focus assertion incorrectly required the list root;
UI Toolkit delegates focus to an internal descendant, now checked within the list.

Limits: no device frame-time, RSS, allocation, native keyboard or screen-reader claim.
Canonical snapshots still copy up to 1000 turns; only UI row creation is virtualized.
The synthetic lab remains opt-in; normal companion chat is session-only. No Editor
layout, Game-view size, package version, asset GUID or portrait setting changed.

Final regression: 12 runtime recovery checks passed. Play restored to stopped;
TalkingCompanion clean with five roots; Console errors empty; credential fixture removed.
Git diff whitespace check passed. Physical-device checks remain unexecuted.
