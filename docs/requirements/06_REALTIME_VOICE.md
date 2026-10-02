# Realtime voice and transport abstraction

## VOICE-01 — two independent boundaries

`IRealtimeTransport` handles connection, microphone/remote tracks, control messages, network quality, mute and disposal. Implement a LiveKit native Unity adapter first, plus a fake deterministic adapter. `IVoiceAgentProvider` handles speech generation, turn detection, transcripts, cancellation, context updates and usage. Support either native speech-to-speech or a streaming STT→LLM→TTS chain through capabilities, not through a misleading single lowest-common-denominator API. A future direct-provider WebRTC adapter must still satisfy server control, consent, metering and safety requirements.

The official [LiveKit Unity SDK](https://github.com/livekit/client-sdk-unity) is the integration reference; pin and test its native mobile dependencies. Server creates room identities and scoped short-lived grants; never accept arbitrary client-selected participant privileges. Media encryption in transit must be enabled. Do not market end-to-end encryption against the processing backend: the voice agent/provider needs to process audio.

## VOICE-02 — lifecycle and turn state

```text
Idle → RequestingPermission → Authorizing → Connecting → Listening
Listening ↔ UserSpeaking → Thinking → AgentSpeaking → Listening
Any active state → Reconnecting → Listening | Failed
Any active state → Ending → Ended
```

Session has one authoritative active generation lease and playback epoch. One realtime session per companion/account by default; second device receives conflict/takeover confirmation, not double spend. A heartbeat renews a bounded lease; server ends orphaned rooms/workers after a proposed 30-second lease timeout. Set join-token lifetime and session limit separately: token expiry alone must never be assumed to disconnect an already connected client.

Use acoustic echo cancellation/noise suppression via tested SDK/OS integration, VAD with bounded endpoint delay, optional push-to-talk fallback and user mute. Distinguish microphone permission, capture active and transport published states. Test noisy rooms, silence, accents, headset echo, music and long pauses. Display interim transcripts as provisional; final transcript is canonical only after a turn is finalized.

## VOICE-03 — interruption, reconnect and consistency

When user speech interrupts: stop local playout immediately, cancel provider generation, increment playback epoch, flush queued audio/visemes, and truncate provider context to the portion actually heard where supported. Store heard-duration plus interrupted status. If provider cannot truncate, rebuild next-turn context from canonical heard content; do not pretend unheard words were delivered. Late audio/events from old epochs are ignored.

Retry connection with bounded jitter/backoff, show reconnect status, never replay captured microphone speech automatically. On Wi-Fi↔cellular handover, attempt SDK recovery; if lost lease, create a fresh authorized session and context. A recovery starts with silence until capture is explicitly valid; no duplicate agent greeting or double response. Background/lock, logout, expiry, quota exhaustion and policy kill switch end capture and billing. Worker termination reconciles usage independently of client reporting.

## VOICE-04 — safety, compatibility and cost

Speech-to-speech latency and audio safety have different tradeoffs. Choose a provider mode only after proving its native safeguards and interruption behavior. If pre-playback moderation is required but unsupported, use buffered/chunked or cascaded TTS mode; do not claim post-transcript filtering prevented already spoken content. Apply the same policy to text and audio. Configure idle timeout, call-length cap, one-session concurrency, admission queue and server enforced budget. Explain gracefully before ending at a limit; never drop an active call merely to display a paywall without context.

Acceptance: device matrix proves two-way audio, Bluetooth route change, echo handling and long-session stability; p95 interruption-to-silence ≤250 ms on reference network; fresh join p95 ≤3 s and successful recovery p95 ≤5 s on transient recoverable loss. Duplicate connect/end requests create one session and one usage settlement. Disconnect or crash cannot run a paid orphan indefinitely. Logs and recordings contain no raw audio by default.
