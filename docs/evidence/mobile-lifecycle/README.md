# Mobile lifecycle and navigation checks

2026-10-07, Unity 6000.5.9f1 Windows Editor, TalkingCompanion scene.
Run `Companion.Editor.MobileLifecycleChecks.Run()` in Play mode through the connected
Unity command tool. `checks.txt` records 20 assertions. Uses a silent synthetic audio
clip, an unsent loopback request, actual character updates, UI navigation events and
wardrobe controls. No microphone permission or recording is triggered by this test.

Verified background interruption/ownership cleanup, disabled actions, frozen body,
camera-state restoration, Unicode in-memory draft, 20 idempotent pause/resume cycles,
no automatic work on resume, Settings/history Back, changed wardrobe preview rollback,
expanded chat collapse and root Back preserving the draft. Initial synchronous swatch
check was replaced with staged UI updates so preview selection is asserted first.

ConversationPolishChecks also passed 56 portrait, keyboard-inset and interaction checks.
Those captures remain in ../ui-polish. No Editor layout or Game-view changes.

Limits: Editor-invoked lifecycle callbacks do not prove real OS microphone shutdown,
Android native/predictive Back or keyboard ordering, phone-call/Bluetooth audio focus,
permission revocation, process death, IL2CPP compatibility or device resource savings.
Session-only drafts are not persisted across process death. M1 remains partial.

11 real local speech/replay checks also passed (../m1/replay/editor-checks.txt).
First run failed because Ollama was stopped; the installed qwen3:8b service was started
and the rerun passed. That first failure remains in the Editor Console as historical
evidence; it was not cleared to claim an empty Console. Initial/final Play=False.
