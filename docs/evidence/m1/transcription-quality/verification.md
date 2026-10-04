# English transcription trial — 2026-10-03

Owner reports intermittent mishearing and confirms English. No real microphone audio or
expected/actual phrase was supplied. Do not mark that exact issue verified fixed.

Implemented: local faster-whisper 1.2.1, Whisper-small pinned to model revision
536b0662742c02347bc0e980a01041f333bce120, CPU/int8/four threads, Python 3.11 environment
with dependency lock. Model and environment stay in ignored artifacts. Runtime is offline,
16 kHz mono PCM via stdin; audio is not saved. DC removal, maximum 8x quiet-input gain,
VAD and separate decoding across long pauses. English language is explicit. Whisper
review state and quiet/clipped/uncertain hints preserve editable text and separate Send.

Passed:

- Five generated-audio conditions: clean, 2%-amplitude speech, five seconds of leading
  silence, repeated phrase separated by four seconds, and silence. `after.txt` checks
  exact normalized words, selected engine and quiet/no-signal warnings.
- 11 Unity Editor voice-input checks with Whisper: capture conversion, portrait controls,
  real transcription into editable draft, no auto-send, and cancellation. No microphone
  opened. Evidence copied to `editor-checks.txt`.
- 10 endpoint transcription checks and 12 existing service boundary checks passed.
- Original 36 hashes, metadata/GUID uniqueness and baseline secret-pattern checks passed.
- Quiet generated speech reached the Editor's Whisper review state with the quiet-input
  hint and correct editable draft. Portrait controls fit and screenshot inspected
  (`quiet-review.png`, `ui-check.txt`). Final Console query had no errors/exceptions.
  Play stopped, scene clean and window rectangles unchanged (`editor-state.txt`).

Comparison: both Windows and Whisper produced one word spelling edit (favourite/favorite)
in 38 words from five clean synthetic conversational phrases. See `comparison.txt`.
Both engines passed the original five-condition baseline. No accuracy gain for the
owner's voice/accent can be inferred from these synthetic tests.

Found and corrected: concatenating VAD speech around long pauses caused Whisper to drop
a repeated phrase. Decoding separate utterances fixed the test; rerun passed all five.
The earlier suspicion that three-second Windows timeouts caused this report was not
reproduced by the baseline. Disabled those cutoffs in the legacy fallback, but no timeout
fix is claimed as the demonstrated cause of the owner's issue.

Unexecuted: owner's microphone/accent/background-noise accuracy, physical clipping/device
permissions, Hindi and mobile. Quiet/clipped/uncertain warnings are heuristic and cannot
detect every misrecognition. No LLM rewriting of transcripts. The next test is repeating
the problematic English phrase in the existing scene; review should name Whisper.

Sources: [faster-whisper usage and CPU inference](https://github.com/SYSTRAN/faster-whisper),
[model repository](https://huggingface.co/Systran/faster-whisper-small), and Microsoft's
[initial silence timeout semantics](https://learn.microsoft.com/en-us/dotnet/api/system.speech.recognition.speechrecognitionengine.initialsilencetimeout?view=netframework-4.8).
