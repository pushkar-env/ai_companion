# P01 — local connection isolation and fail-closed configuration

2026-10-08; Unity 6000.5.9f1 Windows Editor; pinned .NET SDK 10.0.301.

Implemented a pure immutable connection snapshot and connection-source boundary.
The existing session-file reader is compiled only for Windows Editor; players get an
explicit unconfigured source. No hosted service or production identity is implied.
Local endpoints must use canonical 127.0.0.1 HTTP origin syntax, no path/userinfo/query/
fragment, and a 64-character hex token. Failed refresh clears the old snapshot.
All three local request types now use one factory with bounded timeouts and redirects
disabled. Credential strings are not included in diagnostic ToString output.

Submit/Retry/microphone preflight configuration before changing chat/draft/recording
state. Missing configuration preserves the draft, produces no phantom sent bubble and
does not prompt for microphone permission. Runtime chat no longer reads session files.

## Verification

- [contract-checks.txt](contract-checks.txt): 18 groups passed, including five new
  endpoint/credential/platform/route/refresh groups. Command:
  `artifacts/tooling/dotnet/dotnet.exe run --project tests/contract/Companion.Checks.csproj`.
- [editor-checks.txt](editor-checks.txt): 14 runtime checks passed through
  `ConversationConnectionChecks.Run()`: no phantom send, preserved draft, no audio
  capture/permission, stale credential removal, player guidance and request policy.
- [streaming-checks.txt](streaming-checks.txt): 14 actual local streaming/presence/Stop
  checks rerun after refactoring. First text 31.244s, audio 32.093s, stream done 32.604s;
  15 partial text versions. Functional pass only: this run exceeds release latency
  targets. Cold inference is a hypothesis, not an established cause; investigate under
  P37 with separated cold/warm measurements.
- Unity Console errors empty during verification. Initial/final Play stopped, active
  scene clean; no layout, Game selection, build target or existing GUID changes.

The globally installed SDK is now 10.0.401; the repository disables SDK roll-forward.
Installed exact 10.0.301 into ignored artifacts/tooling/dotnet using Microsoft's
dotnet-install script. No global.json change or system SDK replacement.

## Limits / next step

This closes P01's local acceptance only. No Android/iOS player compiled or tested.
No full HTTP redirect attack harness was run; runtime request redirect policy was
asserted. Missing configuration is distinguished from a reachable service failing
after admission; this change does not promise delivery/offline persistence.
P02/P03 must add durable normal-chat admission/reconciliation behind a separate
authenticated protocol. The existing local turn-stream service remains development
only, and its synchronous snapshot is not production token refresh or account login.
