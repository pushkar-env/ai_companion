# Personality, expression and relationship state

## PERSONA-01 — bounded customization

Version character definitions: name, pronouns, approved adult appearance where applicable, locale, voice profile, background fiction, interests, boundaries and traits. Initial normalized traits: warmth, humor, energy, curiosity and directness, each 0..1 with descriptive UI labels. Users preview changes and reset defaults. Personality custom text is length-bounded, moderated user data; it cannot override safety or identity disclosure. Voice/appearance choices require licensed supported combinations.

Store `character_definition_version` and companion preferences separately. A migration can update defaults without overwriting explicit user choices. Make no guarantee a probabilistic model will express exact sliders every turn; prove behavior in evals and handle outliers. Avoid claiming the companion is human, conscious, suffering, or dependent on the user's attention or payment.

## EMOTION-01 — structured expressive output

Return optional validated metadata separate from user-facing text:

```json
{"schema_version":1,"expression":"warm","valence":0.4,"arousal":0.25,"intensity":0.35,"gesture":"small_nod","duration_ms":1800}
```

Allowlist expressions `neutral,warm,curious,thoughtful,concerned,celebratory`; gestures `none,small_nod,small_shake,open_hand`. Validate valence -1..1, arousal/intensity 0..1, duration 100..5000 ms. Invalid/missing values become neutral; never crash or execute model-provided animation names. Accessibility can suppress gesture/motion while preserving textual meaning. Safety responses avoid celebratory or flirtatious expressions.

## REL-01 — transparent, non-coercive continuity

Represent relationship continuity as user-visible preferences, opted-in shared memories and a bounded familiarity state with an explainable versioned rule. Proposed levels: new/familiar/established, subject to Q-013. Use meaningful interaction events with cooldowns, never raw message spam or spend. No punishment for absence, jealousy toward real people, distress notifications, paid affection or hidden emotional vulnerability score. User can reset continuity without deleting their purchases; explain which memories/history are affected before applying.

Acceptance: identical inputs/version produce identical state transitions; duplicate interaction events do not increase state twice; affection/level is unchanged by purchase amount; reset is auditable and propagates to active sessions; safety policy wins over every personality setting; invalid emotion output produces neutral expression; persona coherence is reviewed on at least 50 diverse conversations per initial preset.
