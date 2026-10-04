# Interrupted-turn context — 2026-10-03

Unity 6000.5.9f1, portrait TalkingCompanion, local Ollama and Windows synthesized speech.

Passed: 7 checks (`editor-checks.txt`) using real local requests and audio playback:

- First request has empty history.
- Completed speech commits one ordered user/assistant exchange.
- Partial streamed playback does not add the unfinished prompt.
- Stop then Retry sends the prompt once, with only prior completed context.
- Completed Retry adds exactly one exchange.
- After seeding four synthetic completed exchanges, a real new playback removes the
  oldest whole pair and retains the newest, within eight context messages.
- New chat clears completed context.

Repository preservation: 36 original hashes, metadata/GUID uniqueness and baseline
secret-pattern checks passed. Whitespace check passed. No microphone, model downloads,
provider changes, Android builds or persistent conversation storage involved.

Also reran all 14 New chat checks successfully (sibling `new-chat/editor-checks.txt`),
including cancellation and fresh-turn recovery. Final Console error/exception query
was empty. Scene remained clean; portrait Simulator and window rectangles preserved,
Play returned to stopped (`editor-state.txt`).

Reproduce in Play with **Companion > Run Conversation Context Checks**. Visible chat
retains marked interrupted text; only completed exchanges enter later model requests.
This is a local development rule, not a production retention or memory policy.
