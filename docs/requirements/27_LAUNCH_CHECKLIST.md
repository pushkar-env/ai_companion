# Launch checklist

## LAUNCH-01 — release authorization gate

Check boxes only with linked evidence, owner and date in the implementation repository. All start unchecked. A generated ZIP is not a completed launch checklist.

### Product and rights

- [ ] Audience/age, content boundaries, launch countries/locales and device matrix approved.
- [ ] Brand, avatars, clothes, animations, voices, fonts and music have documented commercial rights.
- [ ] All shipping screens, empty/error/offline states, animation/lip-sync and accessibility reviewed on both platforms.
- [ ] AI disclosure, memory controls, purchase terms, restore, support and account deletion are clear and reachable.

### Policy, safety and privacy

- [ ] Terms/privacy/consent/retention and processor/region decisions reviewed by responsible humans.
- [ ] Store privacy disclosures/data safety forms match actual SDK/network behavior.
- [ ] Safety evaluation, reporting/appeals and crisis-resource playbooks approved with named owner.
- [ ] Export/deletion proves propagation to derivatives, active sessions, processors and restored backups.
- [ ] Required age/content ratings and store-policy checks reflect the current release.

### Commerce and identity

- [ ] Organization-owned Apple/Google/RevenueCat accounts, agreements and production product mappings verified.
- [ ] Both store sandboxes pass purchase/restore/refund/renewal/grace/revocation/account-switch tests.
- [ ] Production/sandbox separation and webhook verification/reconciliation tested.
- [ ] Pricing, free quotas, paid allowances, transfer rules and unit economics approved.
- [ ] Auth, linking, revocation, support recovery and deletion flows pass security review.

### Engineering and operations

- [ ] P0 tests, physical-device matrix, AI evaluations and compatibility suites pass on exact release build.
- [ ] Critical/high exploitable security findings resolved; all remaining exceptions accepted with owner/deadline.
- [ ] Performance, two-times forecast load, soak, provider quotas and cost caps verified.
- [ ] Backups, restore/tombstone replay, incident response, deploy drain, kill switches and catalog rollback rehearsed.
- [ ] Dashboards/alerts, on-call coverage, support contact and escalation ownership ready.
- [ ] No server secrets or mock production integrations in signed APK/AAB/IPA/admin assets.
- [ ] Symbols, SBOM, release provenance and sanitized evidence archived.

### Distribution

- [ ] Store listing, screenshots, ratings, support/privacy URLs and review/demo access accurate.
- [ ] Signing/provisioning, bundle/application IDs, deep links and pushes verified in distribution builds.
- [ ] Current Apple/Google review policies and regional billing requirements rechecked; reviewer questions resolved.
- [ ] Closed beta metrics satisfy agreed thresholds; no Sev0/Sev1 issues remain.
- [ ] Named release owner explicitly authorizes exact environment/build/cohort and rollout budget.
- [ ] Canary health gates pass before each staged expansion; monitoring and rollback operator staffed.

Acceptance: no unchecked mandatory item at public release; any genuine non-applicability has written rationale/owner approval; publication is never inferred from implementation authorization.
