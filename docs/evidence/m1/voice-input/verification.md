# Local voice input verification — 2026-10-02

Windows Unity 6000.5.9f1, portrait Simulator 1170×2532; Node 24.12.0;
installed English recognizer MS-1033-80-DESK. Two microphone devices enumerated without
opening either one. No new packages/models, cloud services or Android builds.

Passed:

- `node tests/e2e/check-local-transcription.mjs`: **10 checks**, in `service-checks.txt`.
  Auth, format/base64/duration/shape bounds, silence, real recognition of generated English
  speech, review marker and confidence. Synthetic phrase recognized as “Please tell me a
  short story about the sunshine”. Fixture PCM stays under ignored artifacts.
- **11 Editor checks**, `editor-checks.txt`: mono conversion/downmix/duration bounds,
  unavailable-device fallback without capture, compact portrait controls, no automatic
  recording, actual HTTP recognition into editable text, no auto-send and cancellation.
- **19 existing real conversation checks** rerun: speech, smoothed mouth motion,
  expressions, cancellation/retry and portrait layout passed. Results in sibling
  `talking-companion/editor-checks.txt`.
- **12 existing talking-service checks**, plus `npm run check` (TypeScript/client,
  114 voice and 19 loopback checks) passed.
- Repository preservation check: all 36 pre-existing hashes, metadata/GUID uniqueness
  and baseline secret-pattern/ignore checks passed. `git diff --check` passed.
- Inspected `review.png`: transcription is editable, microphone picker and recording/send
  controls fit in portrait with the character visible. Editor layout was not rearranged.

Corrected during testing: synchronous recognizer looping could call Recognize after
audio exhaustion and throw; replaced with bounded asynchronous stream recognition.
Record button initially expanded into transcript space; fixed its height and added a
size assertion. Windows dictation misheard “Hello” as “Tell out” in an initial synthetic
test, so editable review is mandatory and no recognition-accuracy target is claimed.

Unexecuted: physical microphone recording, OS permission denial, unplugging during
capture, twenty-second hardware capture, noisy-room/user-accent quality and mobile/Hindi
speech. Generated PCM tests do not prove those paths. Actual microphone recording starts
only on the user's Record click; Stop/focus loss/pause/disable cancel it. The implementation
does not write microphone audio files or log transcripts.

Manual check: in TalkingCompanion Play Mode select the intended microphone, click Record
voice, speak briefly, Finish & review, correct text, then Send. Test Stop while recording
and transcribing. If the input level stays zero, select the USB microphone instead of a
virtual device and check Windows desktop microphone permission.

Recognition uses [Microsoft's audio-stream recognition API](https://learn.microsoft.com/en-us/dotnet/api/system.speech.recognition.speechrecognitionengine.setinputtoaudiostream?view=netframework-4.8)
with an installed local dictation grammar. This is a development adapter, not the selected
shipping voice stack or continuous duplex conversation.
