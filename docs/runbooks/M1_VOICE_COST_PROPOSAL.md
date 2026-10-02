# M1 voice cost and processing proposal — not approved

Researched 2026-09-26. No vendor account, SDK, paid API call or cloud resource created.
This worksheet advances Q-006; it does not choose a provider, approve data transfers or
set product pricing. USD list rates below exclude taxes, currency conversion and credits.

## Candidate architectures

| Candidate | Preparation recommendation | Main unresolved gate |
|---|---|---|
| LiveKit + Google STT V2 + Gemini 2.5 Flash text + Google Neural2 TTS | First synthetic evaluation candidate: text can be checked before synthesis. This is an engineering preference, not approved policy or measured latency. | Exact English/Hindi voice, streaming and region combination; moderation latency; timestamped lip-sync analyzer |
| LiveKit + Gemini native audio | Secondary comparison for responsiveness; no separate STT/TTS charge in the worksheet | Pre-playout safety, heard-content truncation, preview-model lifecycle, phoneme timing and region |
| Azure Speech in a cascaded chain | Keep as an alternate for its documented viseme facilities | Exact Hindi voice/output support, complete region-specific price quote and native spike |

The existing [capability notes](M1_VOICE_OPTIONS.md) link Unity compatibility and Azure
viseme documentation. No listed voice establishes natural code-switching or a calibrated
CC rig. Native SDK versions remain uninstalled/unpinned pending the integration decision.

## Dated rate inputs and reproducible arithmetic

| Meter | Public rate used | Source |
|---|---:|---|
| Google STT V2 standard, first volume tier | $0.016 / processed audio minute | [STT pricing](https://cloud.google.com/speech-to-text/pricing) |
| Google Neural2 synthesis | $16 / million characters after free allowance | [TTS pricing](https://cloud.google.com/text-to-speech/pricing) |
| Gemini 2.5 Flash standard text | $0.30 input / $2.50 output per million tokens, output includes thinking | [Gemini rates](https://ai.google.dev/gemini-api/docs/pricing) |
| Gemini 2.5 Flash native audio preview (12-2025) | $3 audio input / $12 audio output; $0.50 text input / $2 text output per million tokens | [Gemini rates](https://ai.google.dev/gemini-api/docs/pricing) |
| LiveKit agent session, Build/Ship list rate | $0.0100 / minute | [LiveKit pricing](https://livekit.com/pricing) |

**Illustrative usage only, not a measured conversation or invoice:** a 10-minute agent
session, 10 minutes mono audio actually processed by STT, 3,000 synthesized characters,
10,000 aggregate input text tokens across turns, and 1,000 billed output tokens.
Cascade subtotal = 10×0.01 + 10×0.016 + 3000×16/1e6 + 10000×0.30/1e6
+ 1000×2.50/1e6 = **$0.3135**. Sending only five minutes of audio would reduce that
subtotal to $0.2335. Silence sent to STT is not automatically free.

For a separate native-audio usage example, assume the same 10-minute agent session,
20,000 billed audio input tokens, 10,000 audio output tokens, 10,000 text input tokens
and 1,000 text output tokens: subtotal = 0.10 + 0.06 + 0.12 + 0.005 + 0.002
= **$0.2870**. These token counts are assumptions, not a duration conversion or equivalent
quality/usage to the cascade. They must be replaced with provider usage events, including
reprocessed context. Do not add STT/TTS again to native-audio totals.

Both are **partial variable-cost subtotals**. Add plan minimums/allotments, WebRTC
connections/transfer where applicable, separately hosted workers, moderation, viseme
analysis, logs/storage, egress and taxes after deployment is chosen. Do not double-count
worker hosting already included in a chosen agent service. Unknown items are not zero.
Free credits are excluded to avoid disguising recurring cost. These numbers do not justify
a subscription/free allowance or authorize a paid account.

## Processing and retention findings

India launch does not by itself decide India-only processing. LiveKit network region
pinning is separate from agent, inference and stored data configuration; its DPA describes
observability data in the US or Europe. Pinning media to India therefore cannot support an
end-to-end India-only claim. [Region pinning](https://docs.livekit.io/deploy/admin/regions/region-pinning/),
[LiveKit DPA](https://livekit.com/legal/data-processing-addendum).

The current Google Chirp 3 page lists US/EU multiregions and directs users to its locations
API for the full model/language matrix. An older search extract listed Mumbai preview;
we do not treat that stale extract as current verification. Neural2's regional endpoint
documentation identifies US/EU and US Central1; other TTS families have a different region
matrix. No India-only STT/Neural2 combination is established here.
[Chirp 3](https://docs.cloud.google.com/speech-to-text/docs/models/chirp-3),
[TTS endpoints](https://docs.cloud.google.com/text-to-speech/docs/endpoints).

Gemini's paid-service terms say prompts/responses are not used to improve products;
unpaid-service handling differs. This is not zero retention: its abuse-monitoring page
describes 55-day retention. These are developer-API terms, not a statement about every
Google Cloud product. Exact account/service/DPA and application retention still need review.
[Terms](https://ai.google.dev/gemini-api/terms),
[Abuse monitoring](https://ai.google.dev/gemini-api/docs/usage-policies).

## Decision needed and bounded evaluation plan

Q-006 first needs the owner's processing constraint: India-only across media, inference
and logs, or explicitly reviewed overseas processing for a synthetic development spike.
India-only requires a different verified deployment combination; do not provision the
candidate above on that assumption. Also needed before paid work: owner-approved total
test budget, accounts, exact endpoints/terms and scoped secret configuration outside chat.

Proposed evaluation after those gates: synthetic English and Hindi fixtures only, one
concurrent session, at most six 5-minute sessions (three per language), no user records,
no raw-audio recording by our application, explicit cancellation/late-event tests and
measured unit ledger. This is a proposed test envelope, not an approved financial cap.
Before execution implement server admission/usage limits; estimate worst-case retries,
context and billing lag against the approved cap. Disable any vendor recording/tracing
defaults that conflict with the approved terms. Fail closed on region or budget mismatch.

Measure acoustic latency, cancellation, English/Hindi intelligibility and code-switching,
voice rights, timestamp availability and physical routing later. For now the authorized
work remains local diagnostics and the Android build. No provider recommendation is
production-approved and the complete M1 native-voice exit criteria remain open.
