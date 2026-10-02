# Feature flags, release compatibility and rollout

## FLAG-01 — typed, versioned configuration

Flags: realtime_voice_enabled, memory_enabled, purchases_enabled, new_catalog_enabled, provider_route_version, persona_version, maximum_session_seconds and emergency_generation_disabled. Define each flag's owner, default, scope, expiry, safe fallback, validation and audit history. Sensitive capability gates are enforced by server; client flags are presentation hints. Client cannot bypass disabled generation or raise quotas by modifying remote config.

Use stable server-side cohort assignment with pseudonymous identifiers and explicit experiment exclusion for vulnerable/safety-sensitive flows. Consent, policy, encryption, deletion, ownership checks and moderation are never optional experiments. Do not experiment with coercive engagement. Signed/cached client config has a bounded lifetime and minimum supported schema; offline uses safe defaults. New functionality should fail closed where entitlement/safety depends on fresh server state.

## RELEASE-01 — rollout stages

Internal synthetic test → staff/dogfood → approved closed beta → 1% → 5% → 25% → 100%. Percentages are starting rollout steps, not authorization to publish. Each step needs enough active sessions (initial threshold ≥500 eligible sessions or a reviewed longer small-cohort window) and ≥24 hours covering peak demand, passing health/cost/safety gates; low traffic is insufficient evidence rather than automatic success. Public release requires explicit owner authorization and current store approval.

Pause/rollback on any confirmed Sev0/Sev1 incident, critical safety/privacy regression, crash-free sessions below 99.8%, failed voice admission above 0.5%, p95 latency regression >20% against accepted baseline, entitlement corruption or spend above approved cap. Distinguish statistically noisy small samples, but never wait for significance on data loss/security harm. Operators can disable individual capabilities while preserving read-only history/settings/privacy controls.

Maintain current and previous public client compatibility for at least 90 days unless urgent security action requires a forced upgrade. Backend changes are additive; migrations expand/contract; catalogs remain platform/client-schema compatible. Mobile binaries cannot be instantly rolled back on devices: use server flags, compatible catalog rollback and forward fixes. A critical forced update offers explanation and preserves access to support/privacy obligations.

Acceptance: a production-like drill disables voice within 60 seconds for connected sessions and immediately for new admissions; kill switch cannot be overridden by old clients; restoring prior config/catalog works; old client contract tests pass; every rollout step has timestamp, cohort size, metrics, authorized owner and rollback decision.
