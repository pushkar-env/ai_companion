# Real account API and Unity history view evidence

2026-10-05. Windows; Unity 6000.5.9f1 stopped Editor; PostgreSQL 18.1; .NET 10.

Passed:

- `python tools/check-database.py --api --unity`: 114 groups, including the matching
  run-id Unity bridge; final disposable cluster artifacts/database-tests/run-de8xtlu_.
- `Companion > Run Real Local Account API Checks`: eight checks in [checks.txt](checks.txt).
  Actual PostgreSQL-backed history and SSE reach SyntheticHistoryView. Checks cover loading,
  canonical cancelled text, Hindi/emoji, absence of fabricated assistant reply, Stop,
  retry without duplicate rows, account-switch clearing and cross-owner recovery state.
- `Companion > Run Synthetic Account History Checks`: ten transport/socket checks,
  including JsonUtility terminal-null normalization. See ../unity-history/checks.txt.
- Unity scripts compiled, Console error check empty. Fixture removed and API/database
  stopped on completion. No raw credentials or message-bearing server logs printed.

An initial run failed because JsonUtility maps JSON null string fields to empty strings.
The adapter now normalizes absent cancelled/failed text before applying the canonical
projection. Both the new regression and real API test passed after the fix.

Screen implementation: UI Toolkit, vertical single column, max-width 480, scrollable
transcript, explicit synthetic banner, min-height 44 controls, status and recovery UI.
It is available via the explicit Companion/Open Synthetic Account History menu. That
window was NOT opened automatically. Tests drive the view detached from a panel, so
pixel layout, text shaping/fonts, narrow-screen clipping and accessibility are unverified.
No screenshot/visual approval is claimed. No Play transition, scene/asset rewrite,
orientation change or Editor rearrangement occurred.

Try it using the five-minute fixture workflow in ../../../runbooks/DATABASE.md.
This Editor lab is not a mobile route and has no prompt submission/provider execution.
Unexecuted: runtime host/navigation, safe area/keyboard checks, actual device network
recovery, screen-reader/large-text behavior and production auth. Existing production
identity/provider/retention gates remain; real talking-character chats stay session-only.
