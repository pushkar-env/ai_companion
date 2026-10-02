# Unity 6 Android/iOS client

## UNITY-01 — platform and architecture

Pin a supported Unity 6 release and compatible URP/Addressables/native SDK versions after the M1 physical-device spike. Use IL2CPP release builds on Android/iOS and validate stripping/AOT serialization with all adapters. Do not infer mobile compatibility from Editor or WebGL success. Rendering uses URP with mobile quality tiers, baked/simple lighting, bounded transparency and adaptive resolution/LOD. Select UI Toolkit or uGUI in an ADR based on accessibility, mobile keyboard and team tests; no mandatory mixed UI stack.

Use composition-root dependency injection, feature state machines, cancellation on screen/session teardown, async IO, pooled buffers and explicit ownership of Addressables/audio/native handles. Network callbacks marshal into main-thread view updates; audio callback code must avoid blocking and allocations. Avoid scene-wide singleton coupling. Persistent bootstrap survives scene changes without duplicate audio or auth handlers.

## UNITY-02 — screens and state

Owner direction (2026-09-26): mobile is portrait-only. Use a vertical character/chat layout,
portrait startup and width-based scaling. Keep the avatar visible on narrow displays,
compact it while composing with the soft keyboard, respect safe areas, and keep send,
cancel and recovery controls reachable. M0 verifies Editor portrait sizes and simulated
insets; M1 must verify physical orientation lock and actual Android/iOS keyboard behavior.

Screens: eligibility/consent, account/onboarding, companion creation, home/chat, realtime call, wardrobe/store, memory manager, history, settings/privacy/help. Shared components: skeleton/empty/error states, toast/action feedback, reconnect banner and accessible dialog. Keep text UI interactive during character loading; ship a base character or approved lightweight fallback in the app.

Auth, network, chat delivery, call and purchase each have separate explicit states. Chat state: draft→queued→accepted→streaming→completed/failed/cancelled. Call state follows voice spec. A reducer/view model updates presentation; server events use dedupe keys and monotonically increasing versions. Locally stored history is minimized, encrypted with platform-backed keys, namespaced by account and cleared on logout/deletion. Never display the previous user's data during account switching.

## UNITY-03 — lifecycle and permissions

Request microphone only on user voice action with plain explanation; handle revoked/denied permissions and a settings shortcut. Use native audio session/focus integration for Bluetooth, wired headsets, speaker, interruptions and ducking. Default behavior ends/suspends active capture when app backgrounds or device locks; any background-call feature is a separate approved product/store decision. Never leave a hidden microphone active. Foreground reconnection reauthorizes and checks quota.

Release push/deep links are allowlisted and require auth/resource checks. Resume drafts, not automatic purchases/calls. Minimize rendering while backgrounded or in text-only mode. Handle Android process death, iOS memory pressure and both rotation constraints and safe areas.

Acceptance: physical Android/iOS builds complete all primary flows with AOT; 20 consecutive call open/close cycles leak no tracks/handles; account switching reveals no prior private state; incoming phone call, headset change, permission revocation, backgrounding and network transition leave understandable recoverable states; no UI-blocking network requests.
