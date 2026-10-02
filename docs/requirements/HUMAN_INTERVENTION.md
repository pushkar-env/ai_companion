# Human intervention protocol

## HUMAN-01 — triggers and preparation

| Trigger | Human action needed | Safe work before asking | Dependent action blocked |
|---|---|---|---|
| Audience, age, romance/content boundary, launch countries/languages | Product owner approves scope and restrictions | Configurable onboarding and policy fixtures | Public content behavior and enrollment |
| Privacy, retention, consent, crisis response, data residency | Owner and qualified legal reviewer approve policy and responsibilities | Data inventory, draft flows, deletion implementation with synthetic users | Real-user collection and production policy publication |
| Cloud, AI, voice, identity, analytics, email, push, RevenueCat | Owner selects vendors, terms, regions, DPA where applicable, budget and accounts | Adapter contracts, cost comparison, local simulators | Account creation, spending, real-data transfer |
| Credentials and signing | Owner sets scoped secrets/certificates in approved secret manager | ENVIRONMENT checklist and validation scripts | Authenticated integration/signing |
| Apple/Google developer accounts | Owner supplies legal organization, app IDs, tax/banking, agreements, review contacts | Metadata drafts, sandbox flows, release checklist | Store provisioning, purchases, publication |
| Avatar/voice/skin rights | Owner supplies commercial licenses and consent/provenance | Placeholder rig and import validators | Shipping or imitating third-party likeness/voice |
| Prices, free quotas, refunds, entitlements and transfer policy | Owner approves product matrix and unit economics | Config schema and sandbox fixtures | Production SKU activation and charging |
| Destructive migration/deletion, secret rotation, DNS, production changes | Authorized operator approves exact environment, impact and rollback | Dry run, backup verification, reviewed plan | Irreversible or externally impactful execution |
| High-impact technology commitment outside defaults | Owner accepts cost/lock-in implications | ADR and measured spike | Binding external commitment |

Routine user-requested account deletion in the finished app follows the approved deletion workflow and authenticated user confirmation; it must not wait for the developer to manually approve every request. Infrastructure wipes and ad hoc production deletion still require operator authorization.

## HUMAN-02 — request format

Use: `Decision Q-xxx: <specific question>. Needed because: <consequence>. Options: <2–3 concise options and tradeoffs>. Recommended: <reason>. You need to: <account/configuration/decision action, no secret value>. Blocks: <requirement/milestone>. Meanwhile I can: <safe work>.`

Example: “Q-006: Which provider and region may process conversation data? This determines data transfer and cost. Approve one provider/region and a monthly cap, then add its key to the secret manager. Real voice integration is blocked; I can complete simulated audio and interruption tests.”

Record question status `open`, `answered`, `deferred`, or `superseded`; owner; creation date; affected requirements; options; recommendation; answer reference/date; and resulting ADR. Deferral is not approval. An answer must identify environment/scope when authorizing an operation. Repeat a question only if circumstances materially change.

## HUMAN-03 — launch gates

Before external beta: approved audience, region, provider terms, consent/retention, support owner and spending ceiling. Before payments: approved prices/entitlements, sandbox evidence, account ownership and reconciliation policy. Before public launch: reviewed legal/store materials, signed builds, security review, capacity evidence, on-call ownership, incident rehearsal and explicit release authorization.

Acceptance: implementing agent can explain every blocked action and its prerequisite; secrets never enter the decision log; ambiguous approval results in a narrower follow-up, not broad permission; all independent safe work continues.
