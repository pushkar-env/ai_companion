# Lip-motion correction — 2026-10-02

Owner reported overly fast/unrealistic lips in the playable TalkingCompanion scene.
Inspection found an independent 25 ms attack/release pulse on every phoneme. Adjacent
equivalent vowel shapes repeatedly closed despite continuous speech.

Replaced pulses with blended neighboring poses, short time-based easing, reduced mouth
strength and lower jaw travel. Kept the voice rate and audio-clock cue positions intact.
Interrupt still resets immediately; natural completion/silence release smoothly.

Passed in Unity 6000.5.9f1:

- **8 core motion checks** (`motion-checks.txt`): repeated-vowel continuity, bounded
  frame change, restrained jaw, silence closure, overlapping shape transition,
  immediate interruption, equivalent 30/120 FPS easing and natural-end closure.
- **19 real local speech Editor checks** (`editor-checks.txt`): actual model replies,
  SAPI cues, sample-clock motion, smile/concern, Stop, generation cancellation, Retry,
  natural completion and portrait layout after multiple turns.
- `python tools/check-repository.py`: 36 original hashes, metadata/GUID uniqueness,
  baseline secret-pattern/ignore checks. No scene, model, speech-service or package edits.
- Final Console error/exception query: zero entries. Original Play Mode restored.

This verifies smoother continuous motion mechanically and successful real-speech playback.
It does not certify perceptual realism or calibrated phoneme mapping. Owner comparison
with the same prompts remains useful; the CC mapping/jaw assist is still provisional.
