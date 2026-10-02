using System;
using System.Collections.Generic;
using Companion.Contracts;

namespace Companion.Core
{
    public enum ChatStatus { Draft, Queued, Accepted, Streaming, Completed, Failed, Cancelled }
    public enum MockScenario { Normal, Slow, FailBefore, FailMidway }
    public interface ITextGenerationProvider
    {
        IReadOnlyList<TextEvent> Create(string text, string turnId, MockScenario scenario, int attempt);
    }
    public static class UnicodeText
    {
        public static int Length(string s)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++, count++)
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            return count;
        }
    }
    public sealed class MockTextProvider : ITextGenerationProvider
    {
        public IReadOnlyList<TextEvent> Create(string text, string turnId, MockScenario scenario, int attempt)
        {
            // Fixed script: no inferred emotions, policy decisions, network, or model calls.
            string response = text.IndexOf("evening", StringComparison.OrdinalIgnoreCase) >= 0
                ? "This is a scripted demo. A quiet evening could include a short walk, a favorite meal, and time to unwind. What would you add?"
                : "This is a scripted demo, not a live AI response. We can try a small plan together: pick one thing to enjoy today, then leave a little room to rest. 🌿";
            var events = new List<TextEvent>();
            int seq = 0, offset = 0;
            Action<string, EventPayload> add = (type, payload) => events.Add(new TextEvent {
                event_id = StableId(turnId + ":" + seq), aggregate_id = turnId, schema_version = 1,
                seq = seq++, type = type, occurred_at = "2026-09-25T10:00:00Z", trace_id = "mock-v1", payload = payload });
            add("turn.accepted", new EventPayload { user_message_id = StableId(turnId + ":user") });
            if (scenario == MockScenario.FailBefore && attempt == 0)
            {
                add("turn.failed", new EventPayload { code = "mock_unavailable", retryable = true, partial_text = "" });
                return events;
            }
            string partial = "";
            foreach (var word in response.Split(' '))
            {
                string chunk = (offset == 0 ? "" : " ") + word;
                add("turn.text.delta", new EventPayload { text = chunk, offset = offset });
                partial += chunk; offset += UnicodeText.Length(chunk);
                if (scenario == MockScenario.FailMidway && attempt == 0 && offset > 45)
                {
                    add("turn.failed", new EventPayload { code = "mock_disconnected", retryable = true, partial_text = partial });
                    return events;
                }
            }
            add("turn.completed", new EventPayload { text = response, assistant_message_id = StableId(turnId + ":assistant"), finish_reason = "stop" });
            return events;
        }
        public static string StableId(string value)
        {
            // Deterministic fixture IDs only, never authentication tokens.
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
                var guid = new byte[16]; Array.Copy(bytes, guid, 16); return new Guid(guid).ToString();
            }
        }
    }
    public sealed class ChatSession
    {
        readonly ITextGenerationProvider provider;
        readonly HashSet<string> seen = new HashSet<string>();
        IReadOnlyList<TextEvent> events;
        int cursor, nextSeq, attempt, turnNumber;
        MockScenario scenario;
        public ChatStatus Status { get; private set; } = ChatStatus.Draft;
        public string UserText { get; private set; } = "";
        public string Text { get; private set; } = "";
        public string Error { get; private set; } = "";
        public string TurnId { get; private set; } = "";
        public bool Retryable { get; private set; }
        public bool Busy => Status == ChatStatus.Queued || Status == ChatStatus.Accepted || Status == ChatStatus.Streaming;
        public int Attempt => attempt;
        public ChatSession(ITextGenerationProvider provider) { this.provider = provider; }
        public void Send(string text, MockScenario mode)
        {
            if (Busy) throw new InvalidOperationException("Cancel the active response first.");
            if (string.IsNullOrWhiteSpace(text) || UnicodeText.Length(text) > 8000)
                throw new ArgumentException("Enter 1–8,000 characters, including non-whitespace text.");
            UserText = text; scenario = mode; attempt = 0; turnNumber++; Start();
        }
        void Start()
        {
            TurnId = MockTextProvider.StableId("local-turn:" + turnNumber + ":attempt:" + attempt);
            Text = ""; Error = ""; Retryable = false; seen.Clear(); nextSeq = 0; cursor = 0;
            events = provider.Create(UserText, TurnId, scenario, attempt); Status = ChatStatus.Queued;
        }
        public void Tick() { if (Busy && cursor < events.Count) Apply(events[cursor++]); }
        public void Cancel()
        {
            if (!Busy) return;
            Status = ChatStatus.Cancelled; Retryable = true;
        }
        public void Retry()
        {
            if (!Retryable || Busy || attempt >= 2) throw new InvalidOperationException("No retry available (limit: 2).");
            attempt++; Start();
        }
        public void Apply(TextEvent e)
        {
            if (!Busy || e.aggregate_id != TurnId || seen.Contains(e.event_id)) return;
            if (e.schema_version != 1 || e.seq != nextSeq || e.payload == null) { Fail("Stream mismatch. Retry to start a new mock attempt."); return; }
            var p = e.payload;
            switch (e.type)
            {
                case "turn.accepted": Status = ChatStatus.Accepted; break;
                case "turn.text.delta":
                    if (p.offset != UnicodeText.Length(Text) || string.IsNullOrEmpty(p.text) || UnicodeText.Length(Text + p.text) > 16000) { Fail("Invalid text offset or length."); return; }
                    Text += p.text; Status = ChatStatus.Streaming; break;
                case "turn.completed":
                    if (p.text != Text) { Fail("Canonical text mismatch."); return; }
                    Text = p.text; Status = ChatStatus.Completed; break;
                case "turn.failed": Text = p.partial_text ?? Text; Fail(p.code); Retryable = p.retryable == true; break;
                case "turn.cancelled": Status = ChatStatus.Cancelled; Retryable = true; break;
                default: break; // Additive optional events are ignored safely.
            }
            seen.Add(e.event_id); nextSeq++;
        }
        void Fail(string error) { Status = ChatStatus.Failed; Error = error; Retryable = true; }
    }
}
