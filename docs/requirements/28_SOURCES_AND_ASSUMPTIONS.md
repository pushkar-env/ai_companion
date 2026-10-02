# Sources, assumptions and compatibility verification

Prepared 2026-09-25. The supplied conversation establishes a Unity-oriented 3D companion for Android/iOS, chat/voice, customization, purchases and eventual large scale. The retrieved architecture response was truncated. This package is a self-contained design, not a claim to recover omitted text. Product choices not provided by the user remain in QUESTIONS.md.

## Official references inspected

| Reference | Narrow use in this specification |
|---|---|
| [Unity Addressables package reference](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/packages-all/pack-safe/com-unity-addressables) | Async local/remote asset-loading basis; verify exact Editor/package compatibility |
| [LiveKit official Unity SDK](https://github.com/livekit/client-sdk-unity) | Native Unity integration reference; physical mobile spike required |
| [LiveKit authentication](https://docs.livekit.io/home/concepts/authentication/) | Server-generated scoped connection credentials; recheck chosen SDK APIs |
| [OpenAI WebRTC documentation](https://developers.openai.com/api/docs/guides/voice-webrtc) | Optional direct-provider transport/ephemeral credentials; no fixed model commitment |
| [RevenueCat webhooks](https://www.revenuecat.com/docs/integrations/webhooks) | Authentication, durable handling and duplicate-event protection |
| [pgvector official project](https://github.com/pgvector/pgvector) | Exact/approximate vector-search implementation reference |
| [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) | Store submission review input, including payments/privacy/content; human review required |
| [Google Play AI-generated content guidance](https://support.google.com/googleplay/android-developer/answer/14094294) | AI safety/reporting policy review input |
| [Apple facial blendshape reference](https://developer.apple.com/documentation/arkit/arfaceanchor/blendshapelocation) | Terminology reference; page is client-rendered and was not fully text-readable |

These sources support narrowly described capabilities, not every requirement. Performance targets, proposed schemas, cost equations, retention proposals and architecture decisions are original design requirements. The facial list is a project-defined ARKit-style contract and must be checked against assets/tools; it does not imply native facial capture support or supplied viseme timing. No SDK examples in this package are promises of exact current vendor APIs.

## Implementation-time verification register

Before pinning dependencies, verify Unity 6 release support, URP/Addressables/native audio plugin compatibility, IL2CPP/AOT behavior, Android target/min SDK and iOS deployment/Xcode requirements, CPU architectures and store SDK deadlines. Record tested versions and devices in ADRs/lockfiles.

Before enabling AI, verify exact available model IDs, regions, modalities, cancellation/truncation, safety, retention/training settings, pricing and account rate limits. Before enabling commerce, verify RevenueCat SDK/store support, webhook auth options, product mapping and restore/transfer rules against the selected versions. Before publishing, recheck current store policies and jurisdiction-specific requirements with responsible reviewers. Do not invent compliance certification, provider promises or account access from these documents.

## Assumptions and unresolved risks

- One active companion and generation session are initial simplifications; data supports later expansion.
- Final art/rig/voice licenses, audience, budgets, regions and account setup are unknown.
- Low-latency native audio may not provide pre-playback moderation or viseme timestamps; M1 must prove an acceptable path.
- Unity accessibility and mobile audio lifecycle require device evidence, not editor-only confidence.
- Millions-user scale depends on concurrency, provider contracts, unit economics and operational staffing; no initial infrastructure choice alone guarantees it.

Acceptance: version/provider/store decisions link dated official sources and actual spike evidence; unresolved risk remains visible until tested or explicitly decided.
