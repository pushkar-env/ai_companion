# Runtime history recovery evidence

2026-10-05. Windows, Unity 6000.5.9f1, PostgreSQL 18.1, .NET 10.

Passed:

- **Companion > Run Runtime History Recovery Checks** in Play: 12 checks in
  [checks.txt](checks.txt), using actual UnityWebRequest sockets and an offscreen runtime
  UI. Controlled local fixture simulates truncated SSE/EOF, duplicate replay, 401 and 503.
- Partial terminal output does not checkpoint; reconnect uses the accepted cursor and
  presents one canonical reply. 401 disables Retry and stops network attempts, including
  direct Connect/Stop calls. New session clears/reloads history. Four 503 attempts reach
  the reconnect limit; loaded history remains visible with manual Retry available.
- [Expired-session screen](expired.png) and [temporary-outage screen](unavailable.png)
  visually inspected at 360x640. Disclosure, recovery guidance, retained transcript and
  enabled/disabled actions are readable. These captures test the reusable runtime view;
  the route's Back button/insets were tested in the separate navigation evidence.
- Ten transport/socket Editor checks and eight real-account API/view checks rerun.
- `python tools/check-database.py --api --unity`: 114 passed groups. Final scratch cluster
  artifacts/database-tests/run-856bfl46, stopped; ignored credential fixture removed.

Restored Play to stopped. TalkingCompanion unchanged/clean with five root objects; error
Console empty. Temporary scene/server/render texture cleaned up; no Game-view/layout
change. Fixed guidance is shown instead of raw exceptions, credentials or server bodies.

Limits: runtime authentication expiry is a deterministic HTTP 401 simulation, not a real
identity-provider renewal. Backend token clock expiry is independently exercised by the
API harness. No physical Wi-Fi/cellular switch, provider interruption, 45/40-second timeout
soak, screen-reader, standalone-build or production identity claim. Same local session
history remains in memory after expiry; production cache/privacy rules still require
owner decisions. Next safe work: larger-history rendering/performance and accessibility.
