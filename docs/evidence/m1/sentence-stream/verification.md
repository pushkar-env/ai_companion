# Sentence speech streaming — 2026-10-02

Unity 6000.5.9f1, existing portrait Simulator 1170×2532, local qwen2.5:7b and
Microsoft Zira Desktop. No provider accounts, package updates or mobile builds.

Passed:

- 8 Editor checks: fragmented UTF-8 framing, single consumption, truncation rejection,
  bounded pending frames, sequential real speech playback, playback before stream end,
  immediate Stop and prevention of queued/late restart. See `editor-checks.txt`.
- 11 service checks: bounded sentence splitting/Unicode/decimals, real ordered NDJSON,
  actual PCM/visemes, incremental timing and release of the turn slot after abort.
  See `service-checks.txt`. First audio 888 ms, completion 1321 ms in that request.
- Editor measured first audio 0.898 s and stream completion 1.331 s for a short
  two-sentence reply. These observations are not a benchmark or latency guarantee.
- 19 existing real conversation checks, 11 voice-input Editor checks, 10 local
  transcription and 12 service-boundary checks passed. Generated voice fixtures only;
  no microphone recording was initiated by these tests.
- Repository preservation: 36 original asset/package hashes, metadata/GUID uniqueness
  and baseline secret-pattern/ignore checks passed.

One service test initially ran during an Editor conversation and hit the intentional
single-turn busy guard. Reran with no competing conversation; all 11 checks passed.

Unexecuted: physical microphone, subjective listening acceptance, Hindi speech, mobile
runtime and Android builds. Model output remains buffered before sentence synthesis;
single-sentence replies do not benefit from splitting. Separate utterances can affect
prosody. Manually listen to sentence boundaries and verify Stop/Retry in the existing
TalkingCompanion scene. This remains the local Windows English evaluation adapter.

Corrected an Editor-exit MissingReferenceException caused by already-destroyed mesh
renderers during face reset. Repeated the eight stream checks and Play exit after adding
null guards. Portrait window rectangles match the prior baseline; scene remained clean.
Visual screenshot inspected; controls fit. Git whitespace check passed.
