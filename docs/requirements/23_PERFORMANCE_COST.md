# Performance, reliability and cost budgets

These are initial engineering acceptance targets, not measured results or vendor guarantees. M1 selects the reference device matrix and records feasibility; changes require a reviewed ADR and product approval if experience/support changes. Metrics apply to release builds with production-like configuration, no debugger, warm steady-state services unless marked cold.

## PERF-01 — mobile and interaction budgets

| Metric | Initial release target | Measurement |
|---|---|---|
| Rendering | Sustained 30 FPS minimum tier; optional 60 FPS capable tier | 30min mixed chat/call/wardrobe; p95 frame ≤33.3ms or ≤16.7ms at chosen tier |
| Frame stability | Frames >100ms <0.1%, excluding explicit cold loading transition | Device profiler + user-visible stall captures |
| Memory | ≤600MiB resident steady state and ≤850MiB peak on approved 4GB-class minimum device | OS-native memory tooling; revise downward for lower RAM target |
| Leak check | ≤5% residual memory growth after 50 outfit switches/20 call cycles and cleanup | Same scene/content baseline |
| Cold start | p95 ≤4s to interactive cached/base UI | ≥30 cold starts/device, no full catalog prerequisite |
| Avatar detail | LOD0 ≤70k visible triangles; fallback ≤25k; ≤12 skinned draws for primary avatar | Approved worst-case outfit; tune with measured GPU load |
| Download | Base install target ≤200MB; first optional avatar ≤25MB compressed | Per-platform report; store caps rechecked separately |
| UI feedback | p95 ≤100ms for local tap/state acknowledgment | Input-to-visible response trace |
| Battery/thermal | No OS thermal warning, ≤20% steady FPS degradation after 30min | Fixed brightness/network/ambient; report mWh/min, compare releases |
| Text first accepted visible delta | p50 ≤0.8s, p95 ≤2s | Send→first moderated delta on reference network |
| Voice first audible response | p50 ≤0.9s, p95 ≤1.8s | Actual user acoustic end→audible output, including VAD/safety |
| Voice connect/recover | p95 ≤3s / ≤5s | Approved recoverable transitions; denominator documented |
| Lip sync / interruption | p95 ≤80ms absolute AV offset / ≤250ms silence after interrupt | Audio/video capture and playback markers |
| Core API | p95 ≤300ms, p99 ≤750ms excluding generation/export work | Server ingress→egress; report client end-to-end separately |

If primary provider cannot meet voice safety and latency together, disclose tradeoff and seek scope/target decision; never remove safety checks to hit latency. Warm load tests cannot substitute for cold-start tests. Voice subtitles must track final/partial state and not claim unheard generated text was spoken.

## RELIABILITY-01 — initial SLOs

Monthly core API availability ≥99.9% (about 43.2 minutes error budget in a 30-day month); successful authorized voice connection ≥99.5%; crash-free mobile sessions ≥99.8%; Android ANR-free sessions ≥99.8%. Define eligible requests/sessions, exclude intentional 4xx user errors but include dependency outages/timeouts, and report unknown telemetry explicitly. Billing reconciliation converges within 5 minutes for ≥99% of confirmed, accessible provider changes; poll/reconcile when webhooks are delayed. Privacy jobs meet approved deadlines with zero lost requests. Freeze feature rollout when significant error budget is exhausted.

## CAPACITY-01 — explicit workload model

Illustrative planning assumptions only: 1,000,000 MAU × 20% daily activity = 200,000 DAU; 20% of DAU make one 8-minute call/day = 320,000 voice minutes/day. Average concurrency = 320,000/1,440 ≈222; peak factor 5 gives ≈1,111 concurrent calls. Twenty text turns/DAU/day gives 4,000,000 turns/day, ≈46.3 average turns/s and ≈231.5 at 5x peak. These are different from registered accounts and require owner validation. Also account for simultaneous SSE streams, token rates, bandwidth and provider limits.

M1 measures one worker's safe session capacity; total workers ≥ceil(peak sessions/safe sessions per worker) plus documented headroom. Load generators must separate mocked throughput tests from cost-capped real-provider samples. Before each rollout gate prove forecast load at 2x peak for ≥60min, plus 8-hour baseline soak; include realistic history, catalogs, vector filtering and slow clients. Provider contracted capacity must cover peak, not merely infrastructure throughput.

## COST-01 — metering and admission

Compute contribution cost per active user and subscription cohort, not just tokens:

```text
text_cost = input_tokens/1e6 * input_rate + output_tokens/1e6 * output_rate
voice_cost = provider_audio_units*rate + transport_minutes*rate
             + worker_runtime + optional_STT_TTS_cost
total_cost = text + voice + embeddings + moderation + memory_jobs
             + database + storage + CDN_egress + observability + support
net_receipts = store_receipts - applicable_fees - taxes/refunds
margin = net_receipts - allocated_variable_cost
```

Do not double-count STT/TTS when already included in native audio pricing. Version rates, currencies and usage unit definitions; read live price/contract data at provider selection. No price figure in this document is a quote. Normalize usage with provider request IDs; distinguish estimated/accrued/invoiced values.

Before costly operations atomically reserve bounded units against durable per-user and global budget. Concurrent requests cannot exceed balance; settle actual usage and release unused units. Long calls reserve increments and renew server-side; deny further increments at cap and end gracefully. Reconcile abandoned reservations and delayed provider usage without double charge. Cache accelerates checks but is not the ledger. Global caps include reconciliation lag/headroom to avoid overrun; alert at approved 50/80/100% thresholds, shed free/optional work by approved policy, and never silently charge user overages.

Acceptance: cost model populated from approved rates/forecast before billing launch; ten concurrent quota requests cannot overspend one allowance; stuck workers/reservations are reclaimed; caps stop provider spend within measured lag; every performance/SLO report records sample size, percentiles, device/network and excluded cases.
