# Unity synthetic history adapter evidence

2026-10-05; Unity 6000.5.9f1, Windows Editor, project apps/unity verified through the
project-specific unity_companion MCP. Play mode stopped before and after; no layout,
docking, Game-view, scene or orientation actions. Existing asset GUIDs preserved.

Passed: nine checks in [checks.txt](checks.txt), freshly produced by
`Companion > Run Synthetic Account History Checks`. The menu runs in the stopped Editor.
It creates an ephemeral loopback server, exercises actual UnityWebRequest JSON history
and SSE, interrupts a terminal frame, checks Last-Event-ID, replays duplicate admission,
and verifies one completed reply plus explicit Stop. Server and transport are closed.
No real bearer or account database fixture is used, no provider calls.

Unity compiled all three new scripts; error Console empty before the test. Main adapter
uses Core DTO/projection/UTF-8 decoder plus JsonUtility and UnityWebRequest. New metadata
GUIDs generated only for new scripts. No Unity/package version changes.

Unexecuted: actual PostgreSQL account API from Unity, portrait UI integration, Play-mode
application lifecycle, IL2CPP/physical devices, 429/503 exhaustion, token expiry through
this adapter. Existing .NET/backend 113-check evidence remains historical, not rerun here.
Next: explicit synthetic account/history screen and real local API wiring, preserving
the talking companion's session-only default and Editor layout.

Publication: pre-existing commit 7873ad0 pushed successfully, including LFS assets. Bounded
secret-pattern scan passed for 52 changed source/config/docs; LFS fsck passed. GitHub's
53.11 MB runtime-body size warning did not reject the push. Broad whitespace check found
Unity-generated YAML/meta trailing spaces; these were preserved rather than rewriting
asset metadata. This is not a comprehensive security audit.
