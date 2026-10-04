# Project handoff memory

Updated: 2026-10-04. Read with STATUS.md, DECISIONS.md and requirements/QUESTIONS.md.
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
  Ollama qwen2.5:7b, Windows Zira TTS, audio-clock visemes and CC5 facial expressions.
- Reviewed microphone drafts use locally installed Whisper-small when present; fallback
  Windows recognition is labeled. Accuracy on the owner's real voice remains unverified.
- Sentence speech, Stop/Retry, New chat, setup check, remembered microphone selection,
  Replay and timing diagnostic are implemented. Completed context holds four exchanges.
- Local structured model output requires text/emotion. Recent verification: 7 context,
  11 replay and 11 speech timing checks passed. These do not prove acoustic AV accuracy.
- M0 .NET mock API is separate from the Node/Unity prototype. Historical Windows
  Application Control blocked API execution; do not bypass protections or claim a current
  API pass without rerunning. PostgreSQL foundation does not yet integrate either service.

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
- Remaining backend work: authenticated API with atomic quota/admission integration, persisted
  SSE event history, external worker delivery/identity, privacy operations and memory policy.
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
  prototype/database work was published as 6a2320d on 2026-10-04. Quota follows separately;
  consult Git for current publication state. Preserve unintegrated/untracked owner work
  in `models/cosmos/`, which was excluded from the code push.
- STATUS/evidence distinguish passed, blocked and unexecuted checks. Historical STATUS
  sections are prior snapshots and must not override this current handoff or newer evidence.
