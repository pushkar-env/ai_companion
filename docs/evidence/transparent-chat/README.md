# Transparent full-screen chat

2026-10-07, Unity 6000.5.9f1 Windows Editor. Owner requests full-screen transparent
conversation below the navigation, over a stable full-body character.

TransparentChatChecks.Run() renders isolated offscreen panels at 390x844 and 360x640.
Checks cover overlay area and transparency, unchanged camera after message growth,
scrollback preservation, latest-message return, keyboard scene/camera invariance,
composer safe bounds, wardrobe framing and large text. Screenshots use synthetic
conversation fixtures and simulated insets, not real-user transcripts or device QA.

conversation.png: sparse conversation over the character.
scrollback.png: reading older messages without forced scrolling.
keyboard.png: chat/composer above a simulated 280px keyboard; background unchanged.
compact-large-text.png: larger message text on a small portrait screen.

Old separate-stage/expanded-drawer assertions were changed to the owner's new overlay
acceptance criteria. The accessibility Reduce transparency option still adds message
surfaces only when explicitly enabled. Physical touch, IME and contrast/readability
validation remain pending. Editor docking/Game view and scene GUIDs are preserved.

Final overlay suite: 22 checks passed. The first keyboard-follow attempt exposed a
post-layout timing issue; anchoring now uses the resolved viewport maximum on the
following UI update, and the rerun reports zero distance from the latest message.
56 revised portrait/interaction checks also passed. No Editor preview helper used.

14 actual local streaming/activity and 19 lifecycle regressions also passed (111 total).
Lifecycle no longer has an expanded-chat collapse assertion because that UI no longer
exists; root Back remains non-destructive. Final Editor state restored to stopped,
TalkingCompanion clean with five roots, portrait preserved; Console clear.
