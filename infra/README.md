# Local-first infrastructure

M0 requires no containers or cloud. The Unity scene runs entirely in-process; the optional
loopback .NET API uses volatile synthetic data. No credentials are required.

ADR-003 defers PostgreSQL/pgvector, Redis and object storage until the durable M2 slice,
instead of requiring idle containers for a demo. Before M2, add a version-pinned optional
Compose profile, deterministic migrations and disposable database tests. No migration or
production infrastructure is currently provisioned. Cloud/region/spend remain Q-006/007 gates.
