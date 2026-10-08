# Microphone permission recovery

2026-10-07, Unity 6000.5.9f1 Windows Editor, TalkingCompanion.
`MicrophonePermissionChecks.Run()` uses an injected fake permission adapter and sends
UI Toolkit submit events; it never prompts the real OS or opens a physical microphone.
16 assertions in checks.txt cover first explanation, Continue gating, denial, explicit
settings, Back, late grant after pause, no automatic capture, retained Unicode draft,
360x640 buttons, silent playback interruption and idempotent Android manifest patching.
explanation.png and denied.png are offscreen live UI captures, visually reviewed.

Platform adapters and Android Gradle hook are source implementations only: no mobile
build was produced. Native dialogs/settings, OS-revoked active capture, IL2CPP, native
Back/IME and phone-call/Bluetooth focus are unverified. Editor fixtures do not prove
those acceptance criteria. The existing local speech service remains Windows-only.
See ADR-064 for official API references and next validation scope.

Regression: 20 MobileLifecycleChecks, 56 ConversationPolishChecks and 12
MicrophoneSelectionChecks passed (104 assertions total). Play restored to stopped;
TalkingCompanion clean with five roots; portrait preserved; Console errors/warnings
empty. iOS purpose persisted through AssetDatabase.SaveAssets. Git whitespace check passed.
