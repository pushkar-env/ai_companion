# Local synthetic worker lifecycle - 2026-10-05

Passed: `python tools/check-database.py --api` on PostgreSQL 18.1, **51 database/worker
groups + 23 real HTTP checks**. Output is checks.txt. Disposable loopback scratch cluster,
synthetic users, random temporary credentials; stopped afterward. No Unity changes.

Nine added groups verify:

- Runtime/account role cannot claim work or read lease tokens.
- Two real simultaneous database sessions claim a single pending turn only once.
- Renewal needs a valid current token; completion remains owner scoped; invalid TTL rejected.
- Forced database expiry followed by reclaim issues a different token; old completion fails.
- Over-budget settlement rolls back terminal output and leaves the claim unfinished.
- Winning completed receipt retries after expiry without duplicate usage; no new claim.
- Rollback of a claim transaction leaves work available.
- Real Python subprocess with a non-owner PostgreSQL login claims and completes one labeled
  synthetic answer, then a fresh invocation idles because no work remains.
- Production mode, foreign database host and privileged database login are rejected.

Lease expiry is advanced in the synthetic fixture by the migration-owner test connection;
no long real-time outage, killed provider process or production failover is claimed.
The one-pass worker generates a deterministic [SYNTHETIC] reply and reports zero provider
units because it makes no provider calls. It does not read or print prompt text.

Unexecuted/blocked: external authenticated worker dispatch, real generation, unknown-provider
outcome and billing reconciliation, account cancellation/deletion coordination, production
queues/telemetry/load tests, persisted SSE/history and real-user privacy policy. Existing
trusted internal SQL primitives can bypass cooperative fencing if misused; no complete
role/table privilege separation is claimed. Alita's Editor chat remains session-only.
