# Synthetic history client evidence

Date: 2026-10-05. Windows, .NET 10, PostgreSQL 18.1. Synthetic accounts/content only.

Passed:

- Client/check projects built successfully, without adding package dependencies.
- `python tools/check-database.py --api`: 53 database/worker + 47 HTTP + 13 client checks.
- Projection ignores duplicate replay, preserves terminal state and rejects cursor gaps
  without advancing the checkpoint.
- SSE handles byte-fragmented UTF-8 Hindi/emoji, comments, interrupted frames, maximum
  admitted Unicode payload after JSON escaping, and rejects oversized frames.
- Actual local socket fault: first connection closes during a terminal frame; reconnect
  sends the last applied cursor, ignores replayed admission and applies the terminal once.
- Cancellation stops reconnect; non-loopback endpoint rejected.
- Actual API: hydrate cancelled history, repeat load without duplicates, deny other owner,
  and reconstruct equivalent terminal state from SSE.
- Existing backend/HTTP regressions remain green. Scoped documentation diff check passed.

Final scratch run: artifacts/database-tests/run-m18xpfwt. Cluster stopped; generated API
credential fixture removed. Full sanitized results: [checks.txt](checks.txt).

Initial compile failed on an unnecessary exception-type filter; corrected before the
passing runs. The first frame bound was increased to 131072 characters to accommodate
8000 escaped supplementary Unicode characters; the new boundary test passed.

M2 progress: reusable local client foundation under packages/account-client. It targets
.NET 10, not Unity's runtime. No scene, portrait setting, imported GUID or Editor layout
changed. History/cursors/tokens are memory-only, one client per account/conversation.

Unexecuted: Unity/IL2CPP adapter and portrait account/history UI; client 429/503 exhaustion,
45-second deadline fault, production identity refresh, mobile reconnect/large-history UI.
Production account/provider and real-user storage remain blocked by existing policy gates.
Next: provide a Unity-compatible transport/projection adapter and explicit synthetic-only
UI integration; preserve the current playable session-only companion until verified.
