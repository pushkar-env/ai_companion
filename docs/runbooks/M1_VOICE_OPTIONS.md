# Voice feasibility notes — proposal, not a provider selection

Reviewed 2026-09-26. No account, provider package, paid call or region selected.
Editor-only testing remains the current owner instruction. Native voice is a later gate.

Transport reference remains LiveKit native Unity, as required by VOICE-01. Its official
README lists Android/iOS and tests Unity 2022.3.62 and 6000.3.10; our 6000.5.9f1 still
needs a pinned native-SDK/IL2CPP spike. Git installation requires Git LFS. We have not
installed it. [Official SDK](https://github.com/livekit/client-sdk-unity)

| Candidate approach | Reason to evaluate | Evidence still needed |
|---|---|---|
| STT → moderated text → TTS behind LiveKit | Explicit point to screen generated text before speech; separate speech/LLM providers | Added latency, cancellation, heard-content context rebuild, bilingual ASR/TTS and end-to-end cost |
| Native speech-to-speech behind LiveKit | Evaluate conversational responsiveness | Exact safeguards before playout, interruption/truncation, transcript/audio consistency and timestamped facial cues |

LiveKit documents both pipeline types; the safety/latency assessment above is our
engineering inference, not a vendor guarantee. [Pipeline overview](https://docs.livekit.io/agents/models/)

For a cascaded prototype, Azure Speech and Google Cloud TTS both list Hindi voices.
Voice listing alone proves neither code-switching quality nor India-only processing.
[Azure languages](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts)
and [Google voice list](https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types).
Azure documents synthesis viseme events; exact voice/language and output-format support
must be checked in a spike, not assumed for Hindi or every voice.
[Viseme documentation](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-speech-synthesis-viseme)

Cost worksheet must use the approved model/region/rate/currency, with input/output token
units, ASR seconds, TTS characters/audio units, transport participant-minutes, worker
time, moderation and egress. Native speech pricing must not double-count included STT/TTS.
The [cost and data-region worksheet](M1_VOICE_COST_PROPOSAL.md) now supplies dated public
rates and illustrative calculations. Model, processing scope, measured usage and spend
cap remain unanswered. Neither document is Q-006 approval or a production cost forecast.

Next preparation: verify exact vendor region/data terms and current rate cards for a
bounded synthetic English/Hindi sample; then ask for that concrete provider/region/cap.
Keep credentials in approved secret storage, never chat. No requests sent to providers.
