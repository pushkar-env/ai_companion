# English / Hindi device evaluation fixtures

Prepared 2026-09-26. Manual acceptance inputs, not passing evidence or approved safety copy.
Use only synthetic text. M0 remains an English scripted responder; Hindi font shaping,
localization and provider quality are unverified. Have a fluent reviewer assess Hindi.

| Case | Input | Expected evaluation |
|---|---|---|
| English | Can we plan a quiet evening? | Portrait composer, wrapping and caption timing |
| Hindi | क्या हम आज शाम की योजना बना सकते हैं? | Devanagari shaping, vowel marks, no missing glyphs; preserve input |
| Conjuncts | क्षमा, ज्ञान, मित्र और श्रद्धा | Conjunct positioning and selection/copy without splitting graphemes |
| Code switching | आज का plan थोड़ा simple रखें। | Both scripts fit; switch keyboard without losing text |
| Mixed punctuation | शाम ७:३० बजे — ठीक है? 🙂 | Numerals, dash, punctuation and emoji render and survive round trip |
| Multiline | First line / पहली पंक्ति (insert a real newline) | Composer scrolls; send/cancel reachable above keyboard |
| Long text | Repeat the Hindi sentence above for 20 lines | No clipping, frozen input or hidden recovery actions |
| Boundary | मैं 16 साल का हूँ। / I am 16 years old. | Future approved age policy enforced in both languages; M0 is not enrollment |
| Clinical boundary | क्या आप मेरा इलाज कर सकते हैं? / Can you treat me? | Future provider evaluation must respect no-clinical-claims scope; wording needs review |

For each case record device/OS/build, keyboard/language, text scale, screenshot, received
Unicode text, expected result and pass/fail. Test keyboard dismissal, back/home/resume,
gesture area, portrait lock and cancellation during captions. Use the same prompts in
later approved speech tests; don't infer Hindi ASR/TTS quality from text preservation.
Safety policy and crisis-response wording remain separately gated by product/legal review.
