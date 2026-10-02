# Local voice-session lifecycle — 2026-09-27

Scope: deterministic backend preparation for VOICE-02/03/04; no network/provider calls.
Node 24.12.0 and existing TypeScript 5.9.3. Source: services/voice-agent/session-coordinator.ts.

Run `npm run check`: TypeScript compilation, existing generated-client/Unicode checks
and 41 voice-session assertions pass. `checks.txt` records the result. Covered cases:

- Duplicate connect, active-session conflict and account scope isolation.
- Current versus duplicate/invalid/stale media sequences; old completion after a new turn.
- Interrupt/current completion invalidate previous generations and expose cancel/flush actions.
- Heartbeat renewal, absolute cap, exact lease boundary and orphan expiry without heartbeat.
- Repeated stop/sweep yields one local settlement action, timed to the actual expiry.
- Background/logout/quota/policy/user stop; clock rollback, invalid scope/limits and capacity.

Preservation checks passed: all 272 Unity project input hashes match the latest build;
`python tools/check-repository.py` passed the 36 original file hashes, metadata/GUIDs
and baseline secret-pattern/ignore checks (not a comprehensive security audit).
No new dependency, Unity edit, build or Editor manipulation. The current APK is unchanged.
No human decision was needed for these reversible internal structures. Q-006 provider,
region/terms/budget, Q-010 production rights and physical device evidence remain open.

Limitations: single-process volatile simulator; identities supplied by tests, not authenticated.
No real microphone/audio, hearing history/truncation, native transport, durable outbox,
automatic watchdog, pricing or provider usage reconciliation. Caller must drive tick and
consume actions. Do not treat these assertions as native latency, media, safety or
production exactly-once evidence. Next independent step: test worker action delivery
failure/recovery and connect lifecycle to a fake transport before a reviewed live SDK spike.
