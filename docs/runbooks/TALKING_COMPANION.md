# Play the local talking companion

1. Open `apps/unity/` in Unity **6000.5.9f1**.
2. Ensure Ollama is running with the configured local model. This PC now uses the installed
   **qwen3:8b**, selected in ignored `artifacts/talking-character/model.txt`.
   `COMPANION_LOCAL_MODEL` overrides that file; without either the fallback is qwen2.5:7b.
   Restart the local service after changing the selection. Nothing is downloaded automatically.
3. Choose **Companion > Open Talking Companion**. This opens
   `Assets/Companion/Scenes/TalkingCompanion.unity` and starts the local service.
4. Press **Play**. Keep the existing portrait Simulator view.
   The setup status under the title checks the service and local model. Click it to
   recheck at any time; it preserves your draft and conversation. **Start Ollama** means
   the local engine is unavailable. **AI model missing** means the configured model is
   absent; check the installed model list and `COMPANION_LOCAL_MODEL` before restarting
   the service. Nothing is downloaded automatically. Recognition “configured” does not
   certify microphone input or speaker output.
5. **Alita is the default character**, shown in the header. Settings → **Your companion**
   switches between Alita, Meera, Tara and Arjun; the chat and draft stay, the appearance and
   voice change (Zira for the women, David for Arjun; ADR-074). All use the same local chat
   and microphone-review flow.
   **Style** changes the look and saves it per companion on this device. Arjun is modular
   (ADR-075). His **Outfit** picker offers Signature look (default) and Chambray casual
   (open chambray shirt over a white tee, olive chinos, steel watch). Top, Bottom and
   Accessory allow mix & match; only his own male garments are ever listed. Alita, Meera and
   Tara keep colour and skin-tone choices (Alita also her separates).
   Type a message and click **Send**, or use **Good news**, **Rough day**, or
   **Tell a story**. The character generates a reply, speaks it and animates its mouth,
   eyes/brows and expression. **Stop** cancels a pending reply or silences playback.
   **Retry** resubmits the last prompt.
   **Replay** becomes available when a reply finishes. It plays the same audio, mouth
   cues and expression again without generating a new answer or changing conversation
   context. Stop silences replay immediately. Replay is cleared by New chat, submitting
   the next prompt, or leaving Play; the audio is only held in memory.
   **New chat** in the header stops active work and clears this local conversation,
   draft and Retry prompt. The next message begins with empty conversation context;
   the selected microphone stays unchanged. This does not delete an account or unload
   the local model.
6. For voice input, select your microphone from the dropdown and click **Record voice**.
   Your explicit device choice is remembered on this machine. **Refresh** updates the
   list after connecting a device, without starting capture. If your selected input is
   missing, reconnect it and Refresh or explicitly select another; the app will not
   silently switch to a virtual or different input. Refresh is disabled during recording
   and transcription. New chat preserves the selected microphone.
   Speak in English, then click **Finish & review** (maximum 20 seconds). The review state
   names the recognizer (Whisper or Windows) and warns about quiet/distorted/uncertain
   input when detected. Review/edit
   the recognized words in the text box and click **Send**. Recording never auto-sends.
   **Stop** discards the active recording/transcription. Starting a recording stops
   character speech first; loss of application focus cancels capture.

Local prerequisites on this PC: Node 24.12.0 available on PATH, Windows PowerShell with
System.Speech, the Microsoft Zira Desktop and Microsoft David Desktop voices, Ollama's downloaded model and either the
local Whisper setup below or the offline English Windows recognizer (`MS-1033-80-DESK`
on this PC). The Companion
menu starts Node hidden; startup config is written to ignored
`artifacts/talking-character/session.json`. Never share that file. The service stays
available across Play sessions. No paid API or cloud account. Recording requires Windows
microphone access for desktop applications and a working selected device. No microphone
is opened until you click Record; raw audio is kept in memory, sent only to localhost
for recognition, and is not written to recording files by this implementation.

If the UI reports service unavailable, ensure Ollama is running, choose
**Companion > Start Local Talking Service**, then **Retry**. A first model load can take
longer than a warm reply; requests time out rather than wait indefinitely. Optional
terminal start from repository root: `node services/voice-agent/talking-character.mjs`.
Only start one service for the scene. Ctrl+C stops a terminal-started instance.

Failure recovery now distinguishes busy service (wait briefly), expired connection
(recheck setup), timeout (shorter input), local AI unavailable (start Ollama/check setup),
speech unavailable (check the installed Windows voice), and unusable model output
(Retry/rephrase). Transcription failures ask you to record again or type; the chat Retry
button still resubmits the last chat prompt. Stop remains immediate; no automatic retry.

The voices are installed English Windows TTS (Zira, and David for Arjun), not final character voices. Text is
generated by a real local model, not scripted response fixtures. Emotion is model-selected
from four supported states. Animation mappings and jaw motion are provisional. Speech
starts after the first sentence's audio is prepared, while remaining sentences are
synthesized. Full text generation still happens first. Stop clears current and queued
speech; an interrupted exchange (both prompt and reply) is omitted from next-turn
context. Retry sends that prompt once against the prior completed exchanges. The local
model receives up to four complete exchanges; visible chat can contain older messages.
This prototype has typed and reviewable local
microphone input, session-only context, and no Hindi voice, cloud integration or mobile
runtime support. Windows dictation can misrecognize words; correct the draft before Send.
Do not enter sensitive information; product safety/privacy acceptance is unfinished.

Verification: enter Play, invoke `Companion.Editor.TalkingCharacterChecks.Run()` through
Unity MCP to exercise real conversations; `node tests/e2e/check-talking-service.mjs`
checks the running endpoint without generating turns. Evidence lives in
`docs/evidence/m1/talking-companion/`. Android work is deferred per the owner's request.

Voice input checks: run `node tests/e2e/check-local-transcription.mjs` with the service
running to generate synthetic speech and exercise recognition. Then in Play choose
**Companion > Run Local Voice Input Checks**. This verifies the PCM/transcription/review
path without recording your microphone. Physical microphone quality and permission/
disconnect behavior still need a manual test on your machine.

### Improved local English recognition

This PC now has Whisper-small installed under ignored `artifacts/`; it transcribes locally
using CPU/int8, with no audio upload or runtime model download. If absent, the service uses
the older Windows recognizer and labels it in the review state. Restart the local Node
service after installing Whisper; existing running instances retain their selected engine.
Setup on another Windows machine from repository root (Python 3.11 required):

```powershell
py -3.11 -m venv artifacts/whisper-env
artifacts/whisper-env/Scripts/python.exe -m pip install -r tools/requirements-whisper.lock.txt
artifacts/whisper-env/Scripts/python.exe tools/setup-local-whisper.py
```

Setup downloads dependencies and a pinned public model (~484 MB model file); runtime
recognition is offline. It does not fix clipping or remove all background noise. Check
that the dropdown selects the microphone you intend to use, speak near it, and reduce
gain if distortion is reported. Test the same phrase that was previously misheard.
Accuracy for the owner's voice remains unverified; edit the draft before Send.
`node tests/e2e/check-transcription-quality.mjs` exercises generated speech conditions;
`node tests/e2e/compare-local-recognizers.mjs` compares the local engines on five synthetic
phrases. These scripts never record your microphone.

Sentence streaming checks: run `node tests/e2e/check-sentence-stream.mjs` while no Editor
conversation is active, then in Play choose **Companion > Run Sentence Stream Checks**.
These exercise incremental delivery, real clip playback and cancellation. Listen to a
two-sentence reply to assess pauses/prosody; automated timing checks cannot judge
naturalness. Evidence lives in `docs/evidence/m1/sentence-stream/`.

Speech timing: in TalkingCompanion Play mode choose **Companion > Run Speech Timing
Checks**. This resets the local chat, sends two synthetic two-sentence prompts, replays
the second response and checks cancellation/reset. It does not record a microphone.
Results are saved to `docs/evidence/m1/speech-timing/editor-checks.txt` and `timings.csv`;
the CSV contains timings and chunk counts, never response text/audio. Compare text arrival
to first playback to identify speech-preparation delay, and inspect observed inter-clip
waits when investigating sentence pauses. These are Editor frame-resolution measurements,
not acoustic latency, lip-sync accuracy, cold-start benchmarks or release SLO evidence.

Fresh conversation checks: in Play choose **Companion > Run New Chat Checks**. This
tests reset during speech, transcription and generation, inspects the next request's
empty history, and completes a real fresh conversation. No microphone is opened.

### Alita-only character (2026-10-05)

The saved TalkingCompanion scene is ready to Play with Alita. Her source export stays in
models/alita; Unity assets live under Assets/Companion/Imported/Alita. RuntimeMeshes holds
exact derived meshes retaining all used speech/expression controls with unused morphs
removed. Original/Cosmos Unity imports and prior scene copies live outside Assets under
models/archived-unity. Do not restore archived scene .meta files alongside current ones.

Use **Companion > Check Alita Runtime Meshes** for exact retained-data verification, then
in Play use **Companion > Check Alita Portrait and Face**. Existing Reply Replay checks
exercise actual speech and Stop/reset behavior. AlitaPerformanceChecks.Run("label") via
Unity MCP records 30 seconds idle and 30 seconds cached speech without changing layout.
Evidence: docs/evidence/m1/alita-polish. These are desktop Editor measurements; the rig
still requires mobile LOD/material consolidation and physical-device performance testing.
