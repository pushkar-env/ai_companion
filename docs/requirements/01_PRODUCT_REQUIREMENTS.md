# Product requirements and consumer experience

## PROD-01 — core journeys (P0)

Onboard with concise AI disclosure, approved age eligibility and terms/privacy consent; offer a skippable local demo without unnecessary permissions. Name and customize a companion, preview its voice, and start a conversation. Users can send text, dictate a message, or explicitly start/end realtime voice. The interface shows whether the companion is listening, thinking, speaking, reconnecting or offline. Voice cannot start merely because the app opens.

Conversation history persists across authenticated devices. Users can retry failed sends without duplication, cancel a response, copy text, report a response, manage memory, and delete a conversation/account. Show progress and useful recovery at every external dependency. A pending chat is visibly pending, never presented as delivered. Offline permits cached avatar, wardrobe preview and history; draft sends wait for confirmation/connectivity and never claim an AI response occurred.

Acceptance: a first-time eligible user reaches their first successful text interaction without paying or granting microphone permission; denial has an accessible text path. Killing the app during send preserves the draft/status and retry produces one canonical message. Reporting is reachable from each assistant turn in at most two actions. Delete/export and subscription management are findable in Settings.

## PROD-02 — character, wardrobe and commerce (P0)

Offer a cohesive initial art direction, polished idle/gaze/listening/speaking reactions, accessible customization and a wardrobe with owned/available states. Preview before purchase; show localized store price, renewal period, trial conditions and entitlement. Restore purchases and subscription management must be prominent. Locked items can be previewed but not persisted as owned/equipped by a forged client. No purchase changes how much the companion values a user.

Acceptance: all approved combinations pass clipping and performance QA; returning users see their last valid outfit; failed checkout grants nothing and explains recovery; an entitled cross-device user can restore access without repurchase.

## PROD-03 — experience quality and accessibility (P0)

The mobile app is portrait-first and portrait-locked (owner correction, 2026-09-26).
The character remains visible above the conversation on narrow phones, with a compact
presentation during keyboard entry. Chat composer and recovery controls stay reachable
inside safe areas. Landscape desktop mock layouts are not the mobile acceptance baseline.

Use consistent typography, color, spacing, sound and animation. Support safe areas, notches, keyboard avoidance, screen resizing, readable chat selection, long responses and long names. All primary flows need VoiceOver/TalkBack semantics and focus order via tested Unity/native accessibility bridges. Controls have meaningful labels, color-independent status and sufficient contrast; target WCAG 2.2 AA for admin and applicable mobile UI principles. Support large text, reduced motion, captions, haptic/sound toggles and left/right handed one-handed reach where practical. Do not require audio, facial expressions or color alone to understand state.

Acceptance: primary journeys complete with screen reader and 200% text sizing without inaccessible actions or clipped essential text; touch targets at least platform-recommended size (design target 44pt iOS / 48dp Android); no unhandled blank/loading dead ends. Human design review covers all empty, error, offline, purchase and permission states.

## PROD-04 — scope and success

P0: one polished companion family, presets plus bounded personality controls, account/history, text/voice, opt-in memory, wardrobe, approved IAP, safety/reporting, admin, monitoring, support and deletion. P1: more avatar families, additional validated languages, richer contextual animation and optional scheduled reminders. P2: user-created avatars, social sharing, creator economy, AR, multi-companion scenes and cross-region active-active infrastructure. These require fresh scope and safety review.

Track first meaningful interaction, crash-free sessions, voice success, user-rated helpfulness, purchase reliability, memory correction/deletion success and voluntary retention. Product owner sets target activation/retention/conversion values after beta baseline. Do not optimize time spent, emotional dependence or distress-driven spending. Non-goals: medical/therapy diagnosis, emergency response service, deceptive human identity, autonomous real-world actions and an unrestricted sexual-content service.
