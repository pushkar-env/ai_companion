# Open decisions log

Q-001 through Q-003 were answered by the product owner in this implementation conversation (2026-09-25). Other recommended defaults remain proposals only. Stage later questions as dependencies approach; do not send the entire list as one questionnaire.

| ID | Decision / targeted question | Proposed starting point | Blocks | Owner | Status |
|---|---|---|---|---|---|
| Q-001 | What age range and relationship/content boundaries are intended? | Approved product direction: adults-only, non-explicit, no clinical claims | Legal review, enrollment enforcement still required | Product + legal | answered |
| Q-002 | Which countries and languages launch first? | India; English and Hindi | Regional legal review, provider/hosting selection and bilingual validation still required | Product + legal | answered |
| Q-003 | What visual style, brand and avatar rights are available? | Original stylized adult avatar; temporary neutral brand | Production artwork and license/provenance confirmation still required | Product/art | answered |
| Q-004 | Which minimum phones/OS versions must be supported? | Galaxy S23, Android 16 / One UI 8 available; no iPhone; Android build passed | Physical tests owner-deferred; minimum tiers and iOS evidence pending | Product/engineering | open (partially answered) |
| Q-005 | What free allowances, subscriptions, prices and monthly budget are acceptable? | Metered free access plus a capped subscription; no unlimited promise | Paid usage and store products | Business | open |
| Q-006 | Which AI/voice vendors, processing regions and data terms are approved? | One primary provider; same-policy fallback only | Real-user AI/voice | Product/legal/engineering | open |
| Q-007 | Which cloud, identity and telemetry services/accounts may be used? | Managed regional services; OIDC identity | External provisioning | Owner | open |
| Q-008 | What retention, memory opt-in, export/delete SLA and backup expiry are approved? | Proposed durations in privacy spec; raw audio off | Real data and legal notices | Legal/product | open |
| Q-009 | Who owns Apple/Google accounts, signing, support and store review? | Organization-owned accounts | Distribution and IAP | Owner | open |
| Q-010 | Which asset/voice licenses permit commercial use and edits? | Owner confirms rights to supplied assets and authorizes their public repository upload | Future assets/production voice provenance | Art/legal | answered for supplied repository assets |
| Q-011 | Are guest access and account linking required at launch? | Guest trial; account required for saved history and purchases | Guest limits/data lifecycle and identity implementation still pending | Product | answered |
| Q-012 | Are consumables, cross-account transfers or family sharing needed? | Non-consumable cosmetics and subscriptions only | Commerce rules | Business | open |
| Q-013 | What notifications and relationship progression are acceptable? | Explicit opt-in, quiet hours, no guilt or spending-driven affection | Engagement launch | Product/safety | open |
| Q-014 | Who handles safety reports, appeals, emergencies and privacy requests? | Named owner and approved escalation playbook | Beta operations | Owner/safety/legal | open |
| Q-015 | What launch concurrency, reliability tier and support staffing are funded? | Capacity model in performance spec, validate against forecast | Capacity and launch approval | Owner/engineering | open |
| Q-016 | What gender/pronoun/body customization and number of companions ship? | One active companion; extensible identity model | Final content scope | Product/art | open |

## Detailed decision record template

### Q-011 — guest trial approved (2026-10-04)

Status: answered; owner: product. Owner selected “Guest trial; account for saved history
and purchases” in this conversation. Implement guest entry and require authentication
before persisted account history or purchasing. Guest limits, anti-abuse, expiration,
linking/transfer consent and whether a guest transcript can migrate remain unspecified;
do not infer those policies. ADR-038 records the scope. Local database work uses synthetic
account users; public onboarding still requires Q-007 identity and Q-008 data decisions.
This answer does not authorize a provider, real-user retention policy, paid account
creation or external deployment.

### English microphone issue (2026-10-03)

Owner reports intermittent incorrect transcription and confirms they are speaking English.
Selected microphone and an example of expected versus actual words remain unspecified.
Continue local recognizer improvements; this answer does not authorize cloud audio transfer
or select a shipping provider. ADR-030 records the local Whisper evaluation.

### Q-010 — supplied asset upload authorized (2026-10-02)

Owner states they have all rights and explicitly requests uploading all assets so work
can continue on another machine. This authorizes publishing the supplied CC source
exports, imported models/textures and related evidence to the specified public GitHub
repository. Supersedes the local-only repository exclusion in ADR-025. Record this as
owner-provided rights confirmation, not an independent license audit. No further approval
is needed for this upload. Future third-party assets and shipping voice selection remain
separate decisions. ADR-026 covers LFS storage and reproducible checkout.

### Editor-first priority and local evaluation (2026-10-02)

Owner explicitly requested a playable talking-character scene, Editor verification and
then Android work. Q-004 device/build work is deferred for this slice. Existing installed
Ollama qwen2.5:7b and Microsoft Zira Desktop are used for local development under the
reversible technical-choice authority (ADR-023); no download, cloud call, paid account
or shipping-provider approval is inferred. Q-006 remains open for production vendors,
regions and terms. The scene explicitly labels its English-only local speech limitation;
Q-002's English/Hindi product scope remains unchanged. No new owner decision is needed
for the local Editor test; avatar test-use authority remains Q-010.

### Provider selection withdrawn (2026-09-27)

Owner withdrew the preceding instruction to use OpenAI and ElevenLabs and requested
a requirements review/recommendation instead. Q-006 remains open; neither provider
is selected. No provider code, configuration or paid calls were made in that interrupted
turn. Its provider-specific setup questions are superseded; region, terms, budget and
voice-rights decisions will be revisited for the eventual selected evaluation.

### M1 availability answers (created/answered 2026-09-26)

Q-004 build retry, 2026-09-26: owner said “i've freed up space so build can be triggered
as well”. Authorizes another isolated local development build. Physical installation
and interaction tests remain deferred under the preceding Simulator-only instruction.

Q-006 clarification asked 2026-09-26; status open; owner product/legal. The sourced
proposal is ../runbooks/M1_VOICE_COST_PROPOSAL.md. Asked whether processing including
inference/logs must stay within India, whether reviewed overseas processing may be
evaluated for synthetic development tests, or whether to defer provider decisions.
This narrows eligible deployments; no provider account, spend or real-data permission
is inferred from an answer. Vendor/endpoint/terms, test cap and secure credential setup
remain gates before paid integration. Local diagnostics/builds continue independently.

Q-004 scope update, 2026-09-26: after Android tools became available, ADB returned no
connected devices. Owner answered “test in editor only for now using device simulator”.
Use Unity Device Simulator for current tests. Physical installation/device testing is
deferred by the owner; do not keep requesting a USB connection. An isolated development
build was already underway; it is not physical verification or distribution approval.

Q-010 local testing authorization, 2026-09-26: owner said “lets use this character for
our testing for now & build the test scene”. Accepted scope: local separate CC character
test scene and reversible material/controller work (ADR-010). Content-pack identity,
trial/licensed status and shipping rights still pending; do not repeat that question or
block authorized local testing while waiting. No production likeness/voice approval.

Q-010 update, 2026-09-26: owner exported a prebuilt CC5 character into root `models/`
and requested technical assessment. Source assets now available; this supersedes the
earlier "no assets yet" availability answer, not its publication gate. Content/preset
name and trial versus licensed CC5 status requested; response pending. Owner: product/art.
Recommendation: retain source/license evidence before approving shipping. Local import,
rig/texture and deformation inspection completed independently (ADR-009); production
distribution remains blocked on applicable rights, optimization and calibration.

- Q-004; owner: product/engineering; status: open, partially answered. Asked available Android/iPhone models, OS versions, USB access and Mac/Xcode. Recommendation: use owned hardware before selecting minimum support tiers. Owner answered “samsung galaxy s23”, then “android 16 , one UI 8 , iphone not available , also i'll install the android sdk & ndk tools”. Record S23/Android 16/One UI 8 as the first available test target, not the minimum supported device. USB connection and Mac/Xcode availability remain unconfirmed. Installed Unity currently has only Windows build support. Physical Android tests wait for Android Build Support including SDK/NDK/OpenJDK and connected authorized device; physical iOS tests require an iPhone and Mac/Xcode. No instruction to install/provision external services. ADR-008; device matrix in runbooks/M1.md.
- Q-010; owner: product/art; status: answered for current availability. Offered existing licensed assets or continued synthetic diagnostics. Owner answered “No assets yet; continue synthetic diagnostics”. Authorizes local procedural test geometry and generated tone; no third-party likeness, speech voice, or commercial asset rights supplied. Production rig calibration and voice/art publication remain blocked pending provenance. ADR-008.

### Q-017 — mobile orientation (answered 2026-09-26)

Reaffirmed 2026-09-26: owner requires the app/builds to always remain portrait and the
Editor layout to be preserved. ADR-011 adds build/runtime orientation enforcement,
removes automatic preview resizing, and uses isolated Android build snapshots. This
does not authorize Editor window rearrangement or production distribution.

Owner: product. User explicitly requested portrait mode and reimplementation through M0.
Answer: portrait-first mobile application; accepted as portrait-only orientation with
a vertical avatar/chat shell (ADR-007). No further permission needed for reversible UI,
PlayerSettings or preview changes. Existing audience, India/English/Hindi, asset, provider
and legal decisions remain unchanged. Physical keyboard/notch/orientation evidence remains M1.

### M0 answers (created 2026-09-25; recorded 2026-09-26)

- Q-001; status: answered; owner: product/legal. Options offered: proposed adults-only non-explicit/no-clinical scope, discussion of adult boundaries, or undecided audience. Recommendation was the first option to constrain the initial scope. User answer: “Adults-only, non-explicit, no clinical claims.” Applies to intended product scope; no legal signoff or beta authorization. Implication: no youth enrollment, explicit or clinical product behavior. M0 has no real enrollment. ADR-004.
- Q-002; status: answered; owner: product/legal. Initial free-text country/language question recommended one country and English; user chose “India.” Follow-up offered English, Hindi, or both; user chose “English and Hindi.” Implication: bilingual UI/font/input/voice/safety evaluation for India is required before beta. Hosting/data region remains unapproved. M0 English scripted UI is explicitly incomplete for launch. ADR-004.
- Q-003; status: answered; owner: product/art. Options offered original stylized adult/temporary brand, existing licensed art/brand, or undecided; recommended first option. User chose “Original stylized adult avatar; temporary neutral brand.” Implication: use original geometry during M0; no third-party license, likeness or voice rights were supplied. Production rig/asset rights remain Q-010. ADR-004.

Authorization scope for all three: product direction and local implementation only; no accounts, spending, real-user collection, signing, distribution or publication authorized. Independent M0 work continues. Remaining decisions stay open and are staged for M1/M2.

### Template

ID: Q-___; created: YYYY-MM-DD; status: open; owner: ___

Question and why it matters: ___

Options/tradeoffs and recommendation: ___

Blocked requirements/milestones: ___; independent work: ___

Answer and reference: UNANSWERED; answered at: ___; authorization environment/cap: ___

Resulting ADR, implementation and tests: ___; supersedes: ___

Never put API keys, passwords, billing details or personal identity documents in this file. Use secret names and setup status only.

### Supplied Alita/Cosmos scope (2026-10-04)

Owner requests both new characters in the project, swappable with existing features.
Implemented as session-only appearance selection in the local prototype, sharing current
chat and installed voice. No additional input is needed for this testing slice. Q-016
remains open for production roster/customization and companion identity. This request
does not select a voice provider or new personality policy. See ADR-042.

### Arjun supplied character (2026-10-09)

Owner supplied a third Tripo-generated GLB (`3d boy model.glb`, a stylised young man) and asked to
add him like the previous characters, with cloth physics, blendshapes, lip-sync and facial animation,
"alive", polished and realistic in the app. Accepted scope: local prototype integration as the fourth
appearance and first male companion (ADR-074). He is presented as an adult (adult proportions, 1.78 m).
He speaks with the installed Windows David voice; Alita stays the default. Not inferred: a name (the
owner gave none; "Arjun" is a placeholder that can be renamed), commercial-use rights for this Tripo
output (Q-010 future-asset gate), a production roster or identity policy (Q-016), a production voice,
a separate personality, or publication of the new source files. No owner input is needed to continue
local work; confirm the name and the rights before any external distribution.

### Tara supplied character (2026-10-09)

Owner supplied a second Tripo-generated GLB (`girl_character_model.glb`) and asked to rig it
the same way as Meera (cloth/hair/earring physics, IK, face rig with blendshapes) and add it to
the app with the existing features. Accepted scope: local prototype integration as a third
appearance after Alita and Meera (ADR-072); Alita stays default. Not inferred: a name (the
owner gave none; "Tara" is a placeholder that can be renamed), commercial-use rights for this
Tripo output (Q-010 future-asset gate), a production roster/identity policy (Q-016), a separate
personality or voice, or publication of the new source files. No owner input is needed to
continue local work; confirm the name and the rights before any external distribution.

### Meera supplied character (2026-10-08)

Owner supplied a Tripo-generated GLB and asked to rig it (cloth/hair/earring physics, IK,
face rig with blendshapes) and add it to the app with the existing features, reusing existing
rigs/systems. Accepted scope: local prototype integration beside Alita as a second appearance
(ADR-071); Alita stays default. This supersedes the Alita-only roster for the local prototype
only. Not inferred: commercial-use rights for the Tripo output (depend on the owner's Tripo
plan; Q-010 future-asset gate), a production roster/identity policy (Q-016), a separate
personality or voice for Meera, or publication of the new source files. No owner input is
needed to continue local work; confirm rights before any external distribution.

### Current character scope (2026-10-05)

Owner supersedes the prior roster request: keep only Alita, remove the other two models,
then polish and test performance. Current prototype now exposes Alita only; source exports
and archived imports are retained for recovery. This answers current implementation scope,
not Q-016's final production customization/identity policy. No new input is needed for
Editor polish; physical device evidence remains owner-deferred under Q-004. ADR-043.
