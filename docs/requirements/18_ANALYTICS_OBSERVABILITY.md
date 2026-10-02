# Analytics and observability

## OBS-01 — operational telemetry

Instrument API, outbox, workers, provider requests and media session lifecycle with OpenTelemetry-compatible traces/metrics. Propagate trace IDs across API→voice/AI→jobs; use independent pseudonymous user references where necessary. Capture service version, config/prompt version, region, device tier and normalized errors. Never log tokens, passwords, join grants, push tokens, full prompts, chat text, raw audio, memory contents or payment details. Scrub exception payloads and breadcrumbs as well as explicit logs.

Dashboards: request availability/latency by route, DB pool/slow queries, queue age/dead letters, voice joins/turn latency/drop rate/reconnects, provider errors/rate limits, quota reservations/settlement discrepancies, webhook lag, entitlement reconciliation, asset download errors/cache hits, client crashes/ANRs, frame time/thermal tier and deletion backlog. Histograms use bounded labels; never place user/message IDs in metric dimensions.

Define SLO burn-rate alerts and severity-routing with named on-call owners. Alerts must link a runbook, describe user impact and support mitigation (disable voice, route approved provider, pause catalog, rollback config). Vendor outages still count in end-user SLOs; internal dashboards may split dependency causes. Synthetic checks use synthetic users and budget-capped activity, never personal conversations.

## ANALYTICS-01 — product events and consent

Envelope: event_id, event_name, schema_version, occurred_at, app_version, platform, locale, consent_version, pseudonymous subject/session and allowlisted properties. Initial events: onboarding_step_completed, first_interaction_completed, text_turn_completed, call_started/ended, wardrobe_previewed, purchase_started/result, restore_result, memory_action, report_submitted and settings_changed. Properties include duration bucket, result code or item ID, not free text or emotional inferences. Separate operational necessity from optional behavioral analytics; deny optional collection until required consent exists.

Document purpose, retention and owner for each event. Dedupe client retries, handle clock skew, use server truth for purchase/usage metrics and distinguish delivery from user engagement. Experiments must not optimize harmful attachment. Track opt-outs and accessibility failures without re-identifying users.

Acceptance: automated telemetry fixtures containing seeded secrets/private strings yield none in logs/events/crash attachments; duplicate purchase events do not inflate revenue; every P0 failure mode has a dashboard signal/runbook; alert simulation reaches the approved responder; schema validation rejects arbitrary free-text properties and optional analytics stop after opt-out.
