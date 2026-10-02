# Instructions for the implementing Codex agent

## AGENT-01 — autonomy and continuity

Inspect existing code, instructions, branch state and tooling before changing anything. Preserve user changes. Read this package and QUESTIONS.md; establish which milestone is already complete. Implement the smallest coherent vertical slice that advances the product. Do not stop after producing a plan when implementation is requested.

Autonomously choose reversible internal structures, write interfaces, local migrations, tests, mock adapters, accessibility improvements, documentation and bug fixes. Use the technical defaults in this package when they are uncontroversial and record deviations as ADRs. Install ordinary project dependencies only within existing workspace authorization; external accounts, spend and licenses remain gated. Never substitute a mock for a required integration without visibly labeling the limitation.

Maintain `docs/STATUS.md` with current milestone, completed requirement IDs, test evidence, known limitations, next steps and blockers. Maintain `docs/DECISIONS.md` for accepted ADRs and keep QUESTIONS.md current. On resumption, read these and reconcile actual repository state; do not redo verified work unnecessarily.

## AGENT-02 — ask precisely, continue safely

Use HUMAN_INTERVENTION.md before consequential action. First complete safe preparation: produce the config plan, migration preview, deployment diff, purchase mapping or reviewable proposal. Ask 1–3 focused questions at a time, grouped by the next blocking milestone. Describe the required intervention, why it is needed, choices and tradeoffs, suggested default, and exactly what is blocked. Link artifacts that make approval meaningful.

Stop only dependent work while waiting. Continue independent tests, local UI, interfaces and fixtures. Never interpret elapsed time, silence or an unanswered question as approval. Do not repeatedly ask an answered question; record the answer and scope of authority. A user-approved spending cap or environment authorization remains valid within its recorded bounds. Resume affected work after receiving the answer; confirm the result through a safe check.

Never invent a credential, production URL, legal position, store account, identity, consent, provider availability or financial assumption. Never print or commit a secret. Ask users to configure secret storage themselves or use an approved secure flow, not paste secrets into chat.

## AGENT-03 — implementation quality

Use typed boundaries and generated contracts, deterministic migrations, bounded retries, authorization on every resource, versioned configuration and observable failure handling. Keep domain logic outside Unity MonoBehaviours and vendor SDKs. Do not hardcode model names, product prices, hostnames or permissions into scenes. Keep package versions pinned after compatibility tests; do not select “latest” without checking its suitability.

Before marking a milestone done, execute relevant automated checks and physical-device checks where required. If tools, devices or accounts are missing, report the exact unverified criteria and make the milestone status `blocked` or `partial`, never `passed`. Record commands/tools, build identifiers, environment, device/OS, date and outcomes; sanitize logs. Fix relevant failures before moving on. Do not create tests that simply restate implementation.

## AGENT-04 — completion report

Report what works, requirement IDs satisfied, test evidence, remaining risks, next safe task and any intervention required. No public launch, production migration, paid service activation or destructive operation without authorization matching the action and environment. No credentials in documentation or fixtures. No silent removal of acceptance criteria to obtain a green build.

Acceptance: an unanswered billing decision blocks real billing but does not block mock checkout; an approved answer updates QUESTIONS/DECISIONS and implementation resumes; a missing device leaves device verification explicitly incomplete; no test result or external setup is invented.
