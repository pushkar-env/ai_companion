# Playable talking companion — 2026-10-02

Environment: Windows, Unity 6000.5.9f1, existing URP 17.5.0, portrait Simulator
1170×2532, Node 24.12.0, installed Ollama qwen2.5:7b, Microsoft Zira Desktop.
No package/model downloads, cloud inference, paid calls or Android builds.

## Passed

- `TalkingCharacterChecks.Run()` in actual Editor Play Mode: **19 checks**, recorded
  in `editor-checks.txt`. Real generated happy and concerned replies, timed SAPI cues,
  advancing AudioSource sample clock, changing jaw/smile/brow weights, immediate silence
  and neutral reset, pending-generation cancellation, no late playback, natural completion,
  Retry and stable portrait controls/character size across turns.
- Actual service termination produced a visible recoverable error. Restart through the
  Companion menu code followed by Retry completed a fresh spoken reply. AudioSource
  output peak **0.4142763**, Editor audio unmuted, AudioListener volume 1 (`recovery.txt`).
  This verifies a nonzero rendered audio signal; physical speaker listening/perceptual
  voice quality was not independently evaluated.
- `node tests/e2e/check-talking-service.mjs`: **12 checks** covering health, authorization,
  foreign Host/Origin, invalid JSON/history/prompt/body limits and continued availability.
  See `service-checks.txt`; test logs omit tokens and audio payloads.
- `npm run check`: TypeScript/client checks + **114 offline voice / 19 loopback checks**.
- `python tools/check-repository.py`: all **36 pre-existing asset/package hashes**,
  metadata/GUID uniqueness and baseline secret-pattern/ignore checks (not a security audit).
- Inspected `speaking.png` and `conversation.png`: character visible, arms relaxed,
  transcript scrolls, composer/actions remain in portrait. `offline.png`/`recovered.png`
  capture recovery. `editor-state.txt` confirms unchanged window positions and portrait
  configuration relative to the pre-change inspection. New scene left open, Play stopped.
- Final Unity console error/exception query returned zero entries.

## Failures corrected

- Initial 24 kHz Windows synthesis returned cue positions beyond the clip duration.
  Switched to this voice's native 16 kHz output; actual replies now pass cue validation
  and drive the mouth from the playing sample clock. No arbitrary timing stretch.
- Transcript growth initially compressed the character and controls; fixed flex sizing
  and added portrait bounds checks, then reran the complete real conversation test.
- Corrected a syntax error in the new HTTP test runner before its successful run.
  One MCP inspection snippet also had a log argument type error; project code compiled.

## Limits and next work

This separate Editor scene uses actual local AI and speech, not the synthetic diagnostic
tone or fixed mock replies. Four expression states and SAPI-to-CC mappings are provisional.
It buffers the complete reply before speaking, supports typed English input, keeps eight
context messages in memory and omits an interrupted assistant reply from later context.
There is no microphone, Hindi voice, durable history, shipping policy enforcement, native
mobile transport, production authentication or calibrated acoustic lip-sync evidence.
The existing .NET API Application Control block is unchanged; it is not used by this scene.
M1 device/provider exit criteria remain partial. User deferred Android work for this slice.

Local adapter references: [Ollama chat API](https://github.com/ollama/ollama/blob/main/docs/api.md)
and [Windows SAPI viseme events](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer.visemereached?view=netframework-4.8.1).
These inform API/viseme use; they do not certify our particular rig mapping or voice quality.
