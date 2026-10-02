# Heard-response context — 2026-09-28

Passed `npm run check`: TypeScript/generated-client consumer and 85 voice assertions
(41 session, 22 worker, 22 new heard-response). Node 24.12.0; TypeScript 5.9.3.
Run log: checks.txt. Source: services/voice-agent/heard-response.ts.
Preservation checks passed: all 272 Unity input hashes unchanged; 36 original file
hashes, metadata/GUID and baseline secret-pattern/ignore checks passed. These do not
constitute a full security audit.

Checks cover session/epoch isolation, invalid/oversized/regressing playback reports,
incomplete completion rejection, mid-segment interruption, exact Hindi segment boundary,
full playback, omitted unheard/unaligned responses, idempotent finalization, overlapping
alignment rejection, Hindi/emoji grapheme splits and protection from input mutation.
An integrated synthetic fixture passes a response through coordinator/worker/fake
transport, interrupts at 600 ms, clears the fake queue, and rebuilds only
`Hello. नमस्ते। ` while excluding the unplayed continuation. Late media and history
reports are both rejected. These timing values are deliberately synthetic.

No provider, API/network calls, microphone, audio output or account spending occurred.
No Unity files/settings/layout edits or APK rebuild. Physical listening, native audio
latency, Hindi pronunciation, trusted timing conversion and durable history remain
unverified. M1 remains partial; Q-006 and device/rights gates are unchanged.

Next client integration must report the sample-clock playout position before resetting
audio and include its session/epoch identity. Server input must be authenticated and
checked against delivered media; this module alone does not establish that trust.
