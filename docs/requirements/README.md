# AI Companion — implementation specification

Specification version: 1.0 · Prepared: 2026-09-25 · Status: implementation-ready blueprint with explicit decision gates.

Build an excellent Android/iOS consumer app in Unity 6: a customizable 3D companion, persistent text conversations, expressive realtime voice, user-controlled memory, and purchasable wardrobe/subscriptions. This package specifies the product and engineering work; it is not an implemented or certified production application. Acceptance criteria below are requirements to prove during implementation, not claims of completed tests.

## Start here

Give Codex this instruction: “Read AGENT_INSTRUCTIONS.md, HUMAN_INTERVENTION.md and QUESTIONS.md, inspect the repository, and implement the next unblocked milestone from 25_IMPLEMENTATION_PLAN.md. Maintain evidence and open decisions. Ask targeted questions at the documented gates and continue independent safe work.” Copy AGENTS.md into the repository root, or merge its reference into an existing AGENTS.md without replacing existing instructions.

1. Read [agent instructions](AGENT_INSTRUCTIONS.md), [intervention rules](HUMAN_INTERVENTION.md), [open decisions](QUESTIONS.md), and [product scope](01_PRODUCT_REQUIREMENTS.md).
2. Read [architecture](02_SYSTEM_ARCHITECTURE.md), [repository](03_REPOSITORY_AND_DEVELOPMENT.md), [API](11_API_CONTRACTS.md), [events](12_EVENT_SCHEMAS.md), [data model](13_DATA_MODEL.md), and [formal schema seeds](29_FORMAL_SCHEMA_SEEDS.md).
3. Build the client using [Unity](04_UNITY_CLIENT.md), [avatar](05_AVATAR_AND_BLENDSHAPES.md), [voice](06_REALTIME_VOICE.md), [AI](07_AI_GATEWAY.md), [behavior](08_PERSONALITY_EMOTION_RELATIONSHIP.md), and [memory](09_MEMORY_RAG.md).
4. Add [assets/wardrobe](10_ASSETS_AND_WARDROBE.md), [auth](14_AUTH_SECURITY.md), [commerce](15_COMMERCE.md), [notifications](16_NOTIFICATIONS.md), [safety/privacy](17_SAFETY_PRIVACY.md), [telemetry](18_ANALYTICS_OBSERVABILITY.md), and [admin](19_ADMIN_DASHBOARD.md).
5. Operate using [infrastructure](20_INFRASTRUCTURE_SCALING.md), [environment](21_ENVIRONMENT.md), [testing](22_CICD_TESTING.md), [budgets](23_PERFORMANCE_COST.md), and [rollout](24_FLAGS_ROLLOUT.md).
6. Execute [milestones](25_IMPLEMENTATION_PLAN.md), prove [definition of done](26_DEFINITION_OF_DONE.md), and complete [launch checklist](27_LAUNCH_CHECKLIST.md). Recheck [sources](28_SOURCES_AND_ASSUMPTIONS.md) when selecting exact SDKs/providers.

## Conventions and precedence

MUST/SHALL is mandatory; SHOULD is expected unless an ADR explains an equivalent solution. “Proposed” is an explicitly unapproved business choice. Technical defaults may be implemented locally and reversibly; they do not authorize provider purchases or deployment. P0 is public-launch scope; P1 follows launch; P2 is later expansion. A milestone MVP is not permission to omit remaining P0 requirements from production.

Requirement IDs identify testable groups. Each group has acceptance criteria; create individual test IDs and link evidence in the implementation repository. User decisions override this draft; record changed requirements, implications and tests. If documents disagree, pause the affected consequential decision, record it, and resolve explicitly. Security, deletion, authorization and consent must not be weakened silently.

## Package contents and verification

All specification documents are UTF-8 Markdown, including a documented environment template and JSON/SQL examples. API examples are application-owned contracts, not vendor SDK payloads. Implementation must produce executable OpenAPI/JSON Schemas and migrations from them. MANIFEST.json lists document byte sizes and SHA-256 hashes; VALIDATION_REPORT.md describes package-only checks. The archive has one `requirements/` root and no credentials, binaries or external runtime dependencies.

## Build sequence

Local mock vertical slice → physical-device voice/avatar spike → persistent text and memory → real voice and safety → wardrobe and sandbox billing → operational hardening → private beta → authorized store rollout. Release gates are evidence based, not calendar promises. No millions-user reliability claim is allowed before capacity assumptions and tests support it.
