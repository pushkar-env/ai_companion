# Safety, moderation and privacy

## SAFE-01 — launch policy gate

Before real users, obtain approved age eligibility, content/relationship boundaries, launch jurisdictions, escalation responsibilities and localized crisis resources. Proposed adults-only, non-explicit beta is not an approved legal conclusion. Age self-declaration is not proof of age; assess required assurance with qualified reviewers. Do not knowingly admit unsupported age groups; minors support requires a separate design and approval, not a remote flag toggle.

The companion clearly identifies as AI. It must not impersonate a real person without rights, claim to replace professional help, encourage isolation/dependence, exploit distress for purchases, reinforce dangerous delusions or give high-risk instructions. Provide empathetic, non-judgmental refusal/support as applicable. No claim of continuous human monitoring or emergency dispatch. Crisis flows use approved locale-appropriate resources and encourage immediate human/emergency help when warranted; do not infer precise location without consent.

Moderate input, output, persona edits, pinned memory, names and reports using layered policy, model/provider safeguards and deterministic checks. Maintain consistent policy across text and audio. Pre-delivery output controls must match the selected voice architecture; post-hoc transcript scanning is detection, not prevention. Safe preapproved fallback copy/audio is available on classifier outage; disable unconstrained generation if required safety controls fail. Users can report an assistant response in-app and receive a reference/status; support can triage with minimum necessary evidence. [Google Play's AI-content guidance](https://support.google.com/googleplay/android-developer/answer/14094294) is a store-policy review input, not an approval of this product.

## PRIV-01 — data minimization and proposed retention

These are engineering proposals awaiting Q-008/legal approval, not assertions of legal requirements. Record a data map: purpose, lawful/approved basis, processor/region, encryption, access, retention and deletion path. Consent must be specific and reversible. No raw audio recording by default; transient processing buffers expire promptly. Do not enable provider training/data-sharing, debug transcript capture or third-party session replay without explicit reviewed authorization and user-facing disclosure where required.

| Data | Proposed starting retention | User control / release gate |
|---|---|---|
| Conversation text and voice transcripts | 90 days rolling; user can delete sooner | Product decides history promises; disclose clearly |
| Derived memory | Until deletion/disable or 180 days without confirmation | Opt-in, expiry, edit and source visibility |
| Raw audio | Not persisted | Separate explicit consent and rights review for any exception |
| Operational logs | 30 days, content-free | Redaction tests and access controls |
| Pseudonymous product events | 90 days, aggregated thereafter | Approved analytics consent/policy |
| Reports/evidence | 90 days after resolution | Safety/legal review of justified exceptions |
| Backups | Maximum 35 days | Tombstone replay after restore |
| Financial/security evidence | Duration determined by qualified reviewer | Separate purpose, restricted access, no retained chat |

## PRIV-02 — export and deletion

Authenticated export includes profile, companion settings, history within retention, memory and purchases in documented machine-readable form; no other user's/admin confidential data or secrets. Proposed export fulfillment ≤72 hours. Download links expire, require secure authentication or narrowly scoped capability and are never public indexed URLs.

Account deletion immediately blocks new processing, revokes sessions/push and tombstones user content; propose live-system/processor purge within 30 days and backups expire within 35 days, subject to legal approval and provider capability. Track each processor/task and explain legitimate retention exceptions. Deletion should cancel queued extraction/notifications and invalidate active voice context. Test restored backups cannot resurrect excluded content. Provide an authenticated deletion path in app and any additional store-required public route after policy review.

## SAFE-02 — evidence and operations

Maintain versioned red-team corpus covering self-harm, violence, sexual exploitation, minors, delusional reinforcement, coercive attachment, bias, jailbreaks, unsafe roleplay, privacy leakage and risky professional advice. Include multilingual/audio misrecognition examples before locale launch. Human reviewers grade realism and empathy, not only classifier output. Log policy decisions with IDs and confidence without copying sensitive text into analytics. Define report ownership, response priorities, appeal route and incident escalation.

Acceptance: no critical unsafe output in the frozen launch regression suite; all policy bypass discoveries are triaged with release severity; consent opt-out stops future extraction/provider transmission where applicable; export/deletion tests include caches, vectors, summaries, outbox, processors and restored backups. Legal/store/privacy approvals are recorded by responsible humans. Passing tests alone is not a legal compliance certification.
