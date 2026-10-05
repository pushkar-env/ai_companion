# Project handoff memory

Updated: 2026-10-05. Read with STATUS.md, DECISIONS.md and requirements/QUESTIONS.md.
This file records implementation context, not the companion's memory of real users.

## Product and owner constraints

- Adults-only, non-explicit companionship, no clinical claims; India, English and Hindi.
- Original stylized adult avatar direction; temporary neutral brand. Owner confirms
  rights to supplied CC5 assets and authorized their public repository upload.
- Preserve `apps/unity`, all asset GUIDs, portrait-only app orientation, Editor docking,
  window rectangles and Simulator selection. Do not run layout/preview helpers.
- Prioritize a playable Editor conversation. Physical testing and more Android builds
  remain owner-deferred. Galaxy S23 / Android 16 / One UI 8 available; no iPhone/Mac evidence.
- Earlier OpenAI/ElevenLabs request was withdrawn. No shipping AI/voice provider, cloud,
  identity service, production retention policy, prices or spending authorization exists.
- Continue independent local work. Never request secrets in chat or silently treat a
  proposed policy/vendor as accepted. Q-011 answered 2026-10-04: guest trial; account
  required for saved history and purchases. Guest limits/data lifecycle and transfer
  consent remain undecided. Keep durable account data separate from guest sessions.

## Working implementation

- Unity 6000.5.9f1; URP; startup prototype `Assets/Companion/Scenes/TalkingCompanion.unity`.
- Local English development adapter: Node service in `services/voice-agent`, installed
  Ollama (currently installed qwen3:8b), Windows Zira TTS, audio-clock visemes and expressions.
  Ignored artifacts/talking-character/model.txt selects this machine's model; environment
  COMPANION_LOCAL_MODEL overrides it. The repository fallback remains qwen2.5:7b. Local
  requests disable thinking. No shipping provider selected.
- Reviewed microphone drafts use locally installed Whisper-small when present; fallback
  Windows recognition is labeled. Accuracy on the owner's real voice remains unverified.
- Owner now requests Alita only (2026-10-05 continuation). TalkingCompanion and
  CCCharacterTest both use Alita. Original/Cosmos Unity imports, original diagnostic
  materials and former roster tools were moved with their GUIDs to models/archived-unity,
  outside Assets. Source exports remain unchanged. No character picker for a single rig.
- Alita has derived runtime mesh assets retaining exact geometry/skinning and only used
  facial channels; original FBX is intact. 123 mesh, 15 face/UI, 11 Replay and 11 transcription checks passed.
  Facial updates batch targets and skip unchanged weights. 4x portrait RT antialiasing,
  adjusted hair/skin/eye materials, warm/cool lighting, closer framing and dark input fields.
- Desktop Editor performance comparison completed: 30 s idle + 30 s cached speech each.
  Optimized p95 frame 5.702 / 4.379 ms; no >100 ms frames. Referenced mesh/texture sizes
  23.5 / 27.8 MiB. These are asset-reference sizes, NOT app/OS resident memory. Full rig
  is still 90,595 triangles / 24 material slots; mobile LOD/draw budgets remain open.
  Evidence: docs/evidence/m1/alita-polish. Portrait and Editor layout must stay preserved.
- Sentence speech, Stop/Retry, New chat, setup check, remembered microphone selection,
  Replay and timing diagnostic are implemented. Completed context holds four exchanges.
- Local structured model output requires text/emotion. Recent verification: 7 context,
  11 replay and 11 speech timing checks passed. These do not prove acoustic AV accuracy.
- The .NET execution block did not reproduce on 2026-10-04: fresh M0 API build and HTTP
  tests passed without security-policy changes. M0 and Node/Unity remain separate.
- services/account-api is a separate local .NET API using locked Npgsql 10.0.3, temporary
  synthetic-account tokens, non-owner PostgreSQL login and transaction-local RLS context.
  /local/v1 admission/turn lookup passed 23 real HTTP checks; no production OIDC/SSE.

## Current production-directed work

- M1 remains partial. Advancing independent M2 DATA-01/DATA-03 storage foundations with
  synthetic data while device/provider gates remain open.
- `services/api/Database/001_conversations.sql`: PostgreSQL owner isolation, composite
  ownership FKs, one-active-turn constraint and atomic idempotent text admission/outbox.
- `tools/check-database.py`: isolated local PostgreSQL 18 test cluster, random loopback
  port/password, synthetic test users, stopped artifacts; never uses an existing database.
- `002_terminal_outbox.sql` adds atomic completed/cancelled/failed transitions, canonical
  assistant text, expected-version/conflict handling and a database-local status consumer
  with transactional dedupe/projection/acknowledgement. No broker/provider daemon yet.
- `003_quota.sql` adds configurable global/account caps and idempotent reservation/
  settlement. No production allowance/rate is seeded. Expired holds await trusted usage
  reconciliation; no automatic release worker or provider integration exists yet.
- 34 actual PostgreSQL 18.1 groups passed: admission/RLS, terminal races, consumer rollback,
  Unicode, quota concurrency/settlement and crash recovery of replies/dedupe/balances.
  `python tools/check-database.py` reproduces them; clusters are stopped afterward.
- `004_metered_admission.sql` atomically binds turn/outbox/retry key/quota hold; duplicate
  client-message retries reuse the hold. Build services/account-api, then run
  `python tools/check-database.py --api` (34 SQL groups + 23 HTTP checks).
- 005_metered_terminal.sql adds finish_metered_text: atomic reply/outbox/settlement,
  restricted entry for NOLOGIN companion_worker inheriting trusted companion_runtime.
  Actual usage is a trusted-worker input; no client completion route. Existing low-level
  runtime privileges remain trusted SQL internals, not complete privilege separation.
  python tools/check-database.py --api now passes 42 SQL groups + 23 HTTP checks, including
  parallel retries and crash recovery. See evidence/m2/database/metered-terminal.md.
- 006_worker_leases.sql adds claim_local_turn/renew_local_turn/finish_local_turn, with
  worker-only owner-scoped leases, expiry, replacement tokens and atomic completed receipts.
  services/worker/local_synthetic.py performs one bounded pass using installed psql and a
  non-owner login; fixed labeled reply, zero provider units, no polling or AI calls.
  51 database/worker groups + 23 HTTP checks passed. Synthetic expiry recovery is NOT safe
  authorization to retry uncertain paid provider work; external reconciliation remains open.
- 007_event_replay.sql adds atomic per-conversation event cursors on the existing outbox.
  Account API exposes GET conversations/{id}/events and /messages (bounded JSON pages,
  not SSE), plus POST turns/{id}/cancel with expected_version. Owner-scoped read snapshots,
  canonical history, exact cancel retry, and late claimed-worker rejection are verified.
  Holds remain reserved until trusted settlement; expired cancelled leases need explicit
  reconciliation. 53 database/worker + 38 HTTP checks passed; evidence replay-cancellation.md.
  Unity account/history wiring and retention remain unchanged; Editor was not touched.
- EventStream.cs adds /local/v1/conversations/{id}/stream over persisted events.
  Last-Event-ID/after exclusive resume, 50-event batches, four global slots, 500ms poll,
  30-second lifetime bounded by local token expiry. Connections released between polls.
  53 database/worker + 47 HTTP checks passed (100 total); see live-sse.md and ADR-047.
  No Unity changes; next safe work is a synthetic account/history client with reconnect.
- packages/account-client is a .NET 10 synthetic client foundation, not Unity-compatible
  yet. It hydrates event history, projects canonical turn state and follows SSE using
  applied cursors, duplicate suppression and bounded reconnect/cancellation. Session-only,
  no credentials/cursors written to disk. Tests/account-client runs socket faults plus
  actual account API hydration/isolation; check-database.py --api builds it automatically.
  53 database/worker + 47 HTTP + 13 client checks passed; history-client.md and ADR-048.
- Remaining backend work: Unity-compatible account/history transport and portrait UI,
  external provider dispatch/identity, production OIDC, privacy and memory policy.
  Do not connect real-user storage or silently change session-only prototype retention.

## Tooling and verification cautions

- Use `unity_companion` MCP for this project; `unity_ramayanam` is a separate connection.
  Verify Application.dataPath before mutations. Never call an unpinned Unity connection.
- Preserve initial Play state. Check result artifact timestamps: test files update only
  on completion and an old PASS file is not evidence for a newly started run.
- Node turn service allows one active turn; serialize endpoint and Editor conversations.
- Never print ignored `artifacts/talking-character/session.json` (contains bearer token).
  Avoid raw Unity Editor.log; it can contain launch credentials. Use filtered MCP errors.
- Current branch master; origin pushkar-env/ai_companion. Owner authorized push; earlier
  prototype/database work was published as 6a2320d and quota as 9fef9cc on 2026-10-04.
  Account API work is subsequent local work; consult Git for current publication state.
  Alita integration and reversible Original/Cosmos archive are local work, not yet
  published. Preserve their sources and all imported/archived GUIDs.
- STATUS/evidence distinguish passed, blocked and unexecuted checks. Historical STATUS
  sections are prior snapshots and must not override this current handoff or newer evidence.
