# Local-first infrastructure

M0 requires no containers or cloud. The Unity scene runs entirely in-process; the optional
loopback .NET API uses volatile synthetic data. No credentials are required.

ADR-003 deferred idle containers for M0. M2 now has a transactional PostgreSQL bootstrap
migration and disposable local integration tests using installed PostgreSQL 18 binaries.
See [database runbook](../docs/runbooks/DATABASE.md) and ADR-038. No shared database is
modified. Redis, pgvector and object storage remain unprovisioned until needed; no vector
dimension/provider has been selected. Compose packaging and deployment migrations are
still pending. Cloud/region/spend remain Q-006/007 gates.
