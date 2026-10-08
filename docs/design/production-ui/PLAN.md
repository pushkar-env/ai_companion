# Alita production UI plan

Date: 2026-10-05. Status: immersive shell/chat first slice implemented; broader production flows pending.
Owner chose **warm evening room, emerald accents, smoky glass**. Owner requests full-body
Alita, floating transparent UI and familiar messaging interactions. This plan advances
PROD-01/03 and UNITY-02/03 without declaring M1/M2 or launch complete.

Implementation update: runtime full-body framing, room compositing, glass drawer, vector
controls, conditional recovery/replay, multiline composer, recording presentation and modal
settings now exist. See [runtime evidence](../../evidence/ui-polish/README.md) and ADR-057.
The remaining sections describe the target experience; they are not all completion claims.

## The experience

Open into Alita's room. The existing animated 3D character is the focal point, visible
head to shoes. A small glass header identifies Alita as an AI companion. Conversation
floats over the environment in a bottom drawer; it never becomes a separate opaque page
during ordinary chatting. Typing, recording and recovery remain reachable with one hand.

Use familiar messaging conventions: incoming left, outgoing right, restrained timestamps,
rounded bubbles, multiline composer, microphone and send icons, per-message actions.
Create original styling and icons; do not copy WhatsApp branding or imply human presence.
Keep the existing Alita asset and outfit. The concept illustration is not a replacement rig.

![Conversation and recording concept](assets/conversation-concept-v1.png)

Concept limitations: this is generated design art, not a Unity screenshot. Phone geometry,
tap sizes and font metrics require implementation verification. Double-check marks in the
image are incidental; implementation uses truthful text/status icons, never human read receipts.
The recording waveform must use measured input activity, not a fake decorative live signal.

## Layout contract

The room fills the portrait viewport; UI is a separate overlay layer. All positions below
are design units at 390px logical width, with safe-area insets applied first.

| Element | Initial specification |
|---|---|
| Header | 56 high, 16 side inset; name + AI disclosure; history and settings actions |
| Companion stage | Flexible rectangle between header and chat; full bounds plus 8% framing margin |
| Compact chat | Approximately bottom 28–34% including composer; recent exchange scrolls |
| Expanded chat | Up to 55% when keyboard closed; full-body avatar reframed in remaining stage |
| Composer | 56 minimum; 1–4 lines; 48 minimum mic/send targets; bottom safe inset |
| Empty state | One brief greeting and up to three optional prompts, removed after first send |
| Busy state | Small named status and Stop; no permanent row of disabled development buttons |

The percentages are starting targets, not fixed constraints. At 360x640, with large text
or keyboard open, available geometry takes precedence. Collapse recent transcript to one
scrollable message region, remove prompts, reduce header decoration and fit Alita into
the remaining stage. Full-body framing must be preserved even if the figure gets smaller.
For extended reading or 200% text, offer an explicit focused transcript screen; Back returns
to the immersive full-body scene. This is an intentional reading mode, not silent cropping.

Camera calculation: use actual animated renderer bounds, not a hard-coded head offset.
Fit both width and height to the free stage using camera FOV and aspect, with margin for
idle gestures. Stabilize the fit against frame-to-frame bounds jitter. Recalculate on safe
area, keyboard and drawer geometry changes, not by modifying the Editor Game-view selection.
Use short eased changes with reduced-motion instant updates. Feet must stay above the
compact drawer, visibly grounded by a contact shadow. Cap framing changes during gestures.

The generated room is a candidate 2D background plate. Render Alita independently with
alpha and a matching warm key/cool fill. A plate has no depth or contact shadows; implement
a simple floor/shadow receiver or separate contact-shadow layer and validate the composite.
It cannot support free camera orbit or correct parallax. A later 3D room can replace it.

## Visual system

| Token | Proposed value/use |
|---|---|
| Base | #10191C, dark neutral teal |
| Glass | #172326 at 72–88% opacity, increase behind text as needed |
| Primary | #42D9B3 emerald; dark #073D34 fill for sent bubbles |
| Main text | #F4F6F3 |
| Secondary | #B8C8C4; verify against composited background |
| Warm accent | #D9AD77 for environmental highlights, used sparingly in UI |
| Spacing | 4/8/12/16/24/32 |
| Corners | 24 drawer, 18 bubbles, pill composer; one restrained outline |
| Type | 16 body, 14 secondary, 20 heading; English/Hindi font coverage required |
| Motion | 160–220ms drawer/control transitions; reduced-motion equivalent |

These colors are proposals, not measured contrast results. Test contrast on the brightest
and darkest backdrop regions. Use bounded background scrims; readable text is more important
than transparency. Reduced transparency switches to opaque dark surfaces. Avoid stacked
full-screen blur. Start with tinted glass and highlights; add one downsampled shared blur
pass only after profiling on target hardware. UI Toolkit does not get browser backdrop-filter
semantics automatically. Keep UI text, buttons and icons native rather than baked into art.

## Chat and voice behavior

| State | Visible behavior and recovery |
|---|---|
| Draft | Editable multiline text; disabled send for whitespace; preserve while opening settings |
| Sending/accepted | Truthful pending indicator; no completion tick before acknowledgement |
| Thinking/streaming | Label Thinking/Replying, incremental text, accessible Stop |
| Speaking | Speaking label + Stop; Replay on completed response; captions remain readable |
| Failure | Inline explanation and retry on affected turn; retain draft/partial reply context |
| Offline | Clear offline state, cached content where supported; no invented reply or delivery |
| Recording | Explicit mic action; timer + measured waveform, Cancel and Finish & review |
| Transcribing | Progress with cancel; do not send automatically |
| Review | Transcript in editable composer; explicit Send; record again if needed |
| Permission denied | Brief explanation, system settings route when applicable, text input stays usable |
| Background/lock | Stop capture and release resources; never resume capture automatically |

Maintain current local recording limit until the backend/voice policy changes (currently
0.25–20 seconds). Describe it at recording start, not only after failure. Mic is dictation;
do not expose a working-looking call button before realtime call integration exists.
Later live-call mode needs explicit start/end, listening/thinking/speaking/reconnecting,
audio routing, interruption handling and physical-device evidence.

Autoscroll only when the reader is near the latest message. Otherwise show a New message
chip without moving their position. Keep stable message IDs and virtualized history.
Retry must reuse admission identity for production messages; the current local prototype's
Retry calls Submit again and can append another bubble, so it requires adaptation.
Message actions: copy, replay when audio exists, report within two actions. Reporting must
give a real acknowledged result once integrated; no success toast from an unconnected form.
New chat moves to overflow with confirmation when it would discard an unsent draft.

## Navigation and real-user flows

Keep the home surface quiet: chat is home; header opens history and settings, and a small
companion action opens appearance when functional. Avoid a crowded permanent tab bar.

| Flow | Production design requirements | Current dependency |
|---|---|---|
| First visit | AI disclosure, eligibility, language, guest entry, concise onboarding | Approved scope exists; policy/enforcement and Hindi QA remain |
| Guest | Start text without mic/payment; explain session-only behavior | Guest limits/expiry/transfer undecided |
| Sign in/history | Account required for saved history; clear account boundary; loading/retry/empty | Synthetic integration exists; production identity absent |
| Memory | Opt-in, inspect/edit/forget, clear effect feedback | Policy and real-user storage gates remain |
| Appearance | Owned outfit preview, equip confirmation, loading/error | Alita only; compatible outfits/asset QA needed |
| Purchases | Localized terms, price, restore, manage subscription | Catalog, store accounts and commercial decisions pending |
| Settings | Language, text size, motion/transparency/audio, privacy, help, account | Local preferences can proceed; native accessibility needs validation |
| Privacy/help | Export/delete/report/support with trackable outcomes | Approved policy and operations required before release |

Do not show placeholder purchases, dead navigation or lab controls to real users.
Keep current Local preview disclosure while running the PC adapter. Move setup, microphone
device selection and History lab into a clearly identified development settings panel;
preserve functionality and test hooks. Production builds must not load ignored lab fixtures.

## Implementation sequence

1. **Immersive shell and full-body framing.** Extract presentation styling from
   TalkingCharacter.BuildUi into reusable view/styles; retain conversation/audio ownership.
   Add shared tokens, room plate and adaptive character render layer. Preserve all GUIDs.
   Prove full-body layout and alpha/shadow quality before adding blur.
2. **Chat interaction polish.** Native bubbles, multiline composer, mic/send/Stop icons,
   expandable drawer, draft preservation, conditional prompts, accessible menu actions,
   truthfully mapped state and per-turn retry. Retain existing interruption/replay behavior.
3. **Voice and lifecycle polish.** Recording sheet, timer, measured waveform, transcription
   review, permission/denial UI and background cleanup. Distinguish implemented local
   dictation from future live voice and Hindi support.
4. **Production navigation.** Apply the same design system to history/settings/onboarding;
   retain virtualized rows and account-switch clearing. Integrate approved identity and
   storage when ready. Build independent local preference and state fixtures meanwhile.
5. **Release-quality pass.** Hindi shaping/IME, accessibility bridge, keyboard/notches,
   200% text, reduced transparency/motion, device profiling, interruption and adverse states.
   Add memory/appearance/commerce only as complete tested vertical slices.

First implementation slice is 1–2, including busy/error/keyboard layouts, not just an idle
screen. No cloud or store commitment is required for these reversible local changes.
Do not mix in the existing uncommitted history/backend work or overwrite its documentation.

## Acceptance and evidence

- Offscreen Editor captures at 360x640, 390x844, 412x915 and 1080x1920, with notch/home
  insets, simulated keyboard and 100/150/200% text. Preserve the interactive Editor layout.
- Head and shoes visible above compact chat; expanded/keyboard mode reframes without
  clipping essential controls. Long Hindi/English text and long names wrap appropriately.
- Input/send/mic/Stop reachable; drawer has a button alternative to dragging; keyboard
  focus order and screen-reader labels reflect state. No essential color/audio-only cue.
- Verify draft/send/stream/cancel/retry/replay/new chat; microphone finish/cancel/review;
  offline/loading/permission/error; history navigation and account isolation regressions.
- Check scroll position while reading earlier messages and virtualized long transcripts.
- Inspect room/character lighting, hair alpha edges, feet contact and readable glass.
  Profile render texture, transparency and blur separately against the existing baseline.
- Device QA must separately verify native keyboard/IME, portrait lock, safe area, mic,
  screen readers, frame/memory/thermal behavior. Editor screenshots do not close those gates.

Current evidence: repository requirements and existing UI source inspected; both generated
images visually inspected and saved. No new runtime implementation, Unity test or device
verification is claimed by this design deliverable. Existing M1/M2 status stays partial.

## Assets

- [Conversation concept](assets/conversation-concept-v1.png): reference only, not a UI texture.
- [Evening room](assets/evening-room-v1.png): clean candidate environment plate, not yet imported.
- [Prompts and provenance](PROMPTS.md): exact built-in image generation prompts and limitations.

When importing the plate, copy into Assets/Companion/Art with a new stable .meta, inspect
actual dimensions, select bounded mobile texture settings and verify crop at every target
aspect ratio. Keep a single source image and avoid unnecessary duplicate runtime textures.

## Owner layout revision — 2026-10-07

ADR-066 supersedes the split character/chat drawer: a stable full-body scene behind a
transparent conversation overlay below the top nav. Chat scrolls across the available
screen, with a bottom composer; keyboard/history changes never resize the scene.
No chat expand/collapse control. Default message and input surfaces have no fill;
outlined text and fine borders provide definition. Explicit Reduce transparency remains
an accessibility option. See evidence/transparent-chat for current review images.
