# Progressive replies and AI activity

2026-10-07, Unity 6000.5.9f1 Windows Editor; installed qwen3:8b and Windows Zira.

- decoder-checks.txt: 12 deterministic assertions, including every JSON split width,
  Unicode/escape boundaries, invalid output, synthesis during inference and cancellation.
- service-checks.txt: 13 real endpoint assertions, multiple deltas, canonical equality,
  actual PCM/cues, incremental clips and cancellation releasing the turn slot.
- ui-checks.txt: 14 live Unity assertions; actual partial text, early playback, single
  bubble, typing/playback/online/last-seen transitions, cancellation and transcript layout.
- online/typing/partial-text/speaking/last-seen.png: actual offscreen UI captures reviewed.
  The last-seen screen intentionally simulates a failed availability observation.
- Existing ../m1/replay/editor-checks.txt: 11 real audio/replay assertions passed.

Warm Unity run: first text 78ms, first audio 769ms, complete stream 1332ms, 14 observed
text versions. Separate service run was 2279/2799/3200ms. These are local observations,
not device or network latency guarantees. First-sentence synthesis concurrency is also
verified deterministically with a delayed model fixture. Voice remains sentence-chunked.

No transcript/last-seen persistence introduced. Availability is the local AI service,
not a human contact. No native mobile transport, production streaming policy or provider
integration is claimed. Editor layout and portrait-only settings preserved.

Final regressions: 56 portrait/keyboard-inset checks and 20 lifecycle checks passed.
Decoder suite additionally checks bounded long replies and synthesis failure without
false completion. Total: 126 assertions across decoder/service/live UI/replay/layout/
lifecycle suites. Foreground health probes are the only automatic reconnect activity;
voice, capture, turns and history streams still require explicit user actions.

Final state: original stopped Play mode restored; TalkingCompanion clean, five roots,
portrait-only preserved, no Console errors. An existing FindFirstObjectByType warning
is unrelated. service-final-checks.txt records a rerun after restarting the final code.
