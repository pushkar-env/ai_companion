# Infrastructure and scaling

## INFRA-01 — simplest viable production topology

Deploy to one approved primary region using managed container runtime, load balancer/TLS, managed PostgreSQL with pgvector and backups, managed Redis, object storage/CDN, secrets manager and managed realtime transport where approved. API and jobs can share an image with separate process modes; voice workers scale independently and drain active calls on deploy. Use infrastructure as code with separate accounts/projects, state and secrets for staging/production. Approval required before provisioning spend or real-data transfer.

API instances are stateless apart from bounded local caches; use health/readiness endpoints, graceful shutdown, bounded connection pools and centralized durable state. Autoscale by concurrent streams, request latency/CPU and queue age; voice by active sessions and per-session CPU/memory, not HTTP RPS alone. Cap maximum replicas/spend and admission. Avoid scale-to-zero cold starts for latency-critical production paths unless measured to meet SLO.

## INFRA-02 — capacity planning and triggers

Record registered users, MAU, DAU, peak text RPS, active SSE streams, concurrent voice sessions, call-duration distribution, provider token/audio quotas, CDN bytes and DB storage growth. A million accounts is not a million simultaneous calls. Use 23_PERFORMANCE_COST.md equations with product forecasts and load evidence.

Trigger read replicas only when read pressure dominates and stale reads are safe; entitlement/auth/deletion checks stay on authoritative state. Tune queries/indexes before sharding. Partition high-volume messages/events when vacuum/index/retention operations justify it. Separate a module into a service only when measured scaling or ownership warrants it. Multi-region requires explicit residency/routing, failover, consistency and cost design; do not replicate private content to an unapproved region.

## INFRA-03 — resilience and recovery

Target initial RPO ≤15 minutes and RTO ≤4 hours for durable application data under tested backup/restore scenarios, pending funded topology approval. No claim that provider active calls survive regional loss. Use point-in-time recovery, encrypted backups, tested restore and reconciliation from store/provider sources. Restore private data only after deletion tombstones and revocations are replayed. Define whether backups remain in-region and who may decrypt them.

Runbooks: provider outage, compromised key, runaway cost, database failover, Redis outage, stuck queue, duplicate billing, corrupt catalog, voice worker leak, unsafe output, privacy deletion backlog and rollback. Test dependency failure modes in staging. Queue backpressure sheds nonessential extraction/analytics before critical payment/privacy work. Bounded retries prevent a vendor outage from multiplying spend/load. Dead letters alert with clear ownership.

Acceptance: IaC plan is reviewable and environment-separated; staging failover/restore meets measured RPO/RTO; new workers accept sessions while draining workers finish within cap; two-times forecast peak load has ≥30% measured limiting-resource headroom or a documented lower safe admission ceiling; deletion/purchase jobs remain durable through worker/Redis restart.
