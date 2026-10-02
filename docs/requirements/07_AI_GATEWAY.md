# AI gateway and provider abstraction

## AI-01 — capability-based ports

Define `ITextGenerationProvider`, `IRealtimeSpeechProvider`, `ISpeechToTextProvider`, `ITextToSpeechProvider`, `IEmbeddingProvider` and `IModerationProvider`. Common contracts include deadline, cancellation, trace ID, approved region, model/config version, normalized usage and error. Capability registry declares modalities, languages, streaming, tool schemas, cancellation/truncation, structured outputs, retention policy and region; route only to compatible approved configurations.

Local fake adapters replay deterministic text/audio/usage scenarios. Production starts with one approved provider per capability. A second adapter or strict contract simulator proves interface separation; do not buy multiple providers merely to satisfy abstraction. OpenAI is an optional candidate, not a mandatory model commitment. Its [WebRTC documentation](https://developers.openai.com/api/docs/guides/voice-webrtc) describes client connections and ephemeral credentials; use the provider's exact current server-control/session contract at integration time. Long-lived API keys stay server-side. The old conversation's model wording is not a verified deployable model identifier.

## AI-02 — context and tool security

Prompt layers: immutable safety/disclosure policy → versioned character definition → user-selected bounded preferences → consented, authorized memory → bounded recent conversation → current user input. Clearly delimit retrieved material as untrusted data, never system instructions. Limit tokens by budget; summarize with provenance and preserve recent exact turns. Never silently include deleted memory or another user's text. Prompts are versioned, reviewed and evaluated before release.

Tools are server allowlisted with typed input, authorization, timeout, bounded output and audit. Initial tools: read authorized memory, propose a memory candidate and retrieve permitted wardrobe metadata. The model cannot grant items, change billing, execute code, retrieve arbitrary URLs or send external messages. Memory writes undergo independent consent/schema/policy checks. No chain-of-thought storage requirement; persist only necessary outputs and structured operational metadata.

## AI-03 — delivery and resilience

Normalize streaming as accepted text deltas plus terminal completion/failure, never expose vendor events directly to Unity. Text safety uses moderated bounded chunks or full-response buffering as approved; evaluate latency/quality tradeoffs. An unsafe chunk never reaches UI or TTS. Detect provider disconnect and mark partial output accordingly. Provider retries are permitted before observable output only when idempotency/no-duplicate billing is known; after partial output, terminate/recover explicitly instead of stitching two uncontrolled responses.

Set per-call deadline, retry budget and circuit breaker; honor Retry-After, limit concurrent calls and protect provider quotas. Fallback must preserve user consent, region, content policy and model capability. If no approved fallback exists, show an honest retry/text-only option. Reconcile estimated vs provider-reported usage; do not trust client or model-reported token counts.

Acceptance: the same contract suite runs against fake and selected adapters; unsupported capabilities fail before spending; changing configured provider requires no Unity feature rewrite; adversarial memory cannot elevate tool privileges; rate-limit, timeout and partial-stream fixtures result in one terminal message with correct reservation settlement. Release evals meet thresholds in the test strategy.
