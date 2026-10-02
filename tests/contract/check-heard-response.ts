import { HeardResponse } from '../../services/voice-agent/heard-response.ts';
import { LocalVoiceSessions } from '../../services/voice-agent/session-coordinator.ts';
import { FakeVoiceTransport, LocalVoiceWorker } from '../../services/voice-agent/local-worker.ts';
let count = 0;
function check(ok: boolean, label: string) { if (!ok) throw new Error(label); count++; console.log('PASS ' + label); }
function rejects(fn: () => unknown, message: string) {
  let error = ''; try { fn(); } catch (e) { error = (e as Error).message; }
  check(error === message, 'reject ' + message);
}
const timed = [{ text: 'Hello. ', startMs: 0, endMs: 200 },
  { text: 'नमस्ते। ', startMs: 250, endMs: 600 }, { text: 'Unheard promise.', startMs: 600, endMs: 1000 }];
const response = new HeardResponse('s', 1, 1000, timed);
rejects(() => response.context(), 'response_not_final');
check(!response.reportPlayed('other', 1, 1000), 'wrong session cannot advance history');
check(!response.reportPlayed('s', 0, 1000), 'stale epoch cannot advance history');
check(!response.reportPlayed('s', 1, NaN) && !response.reportPlayed('s', 1, 1001), 'invalid or excessive playout rejected');
check(response.reportPlayed('s', 1, 450), 'valid playout accepted');
check(!response.reportPlayed('s', 1, 200), 'regressing playout rejected');
rejects(() => response.finish('completed'), 'playout_incomplete');
const partial = response.finish('interrupted');
check(partial.heardMs === 450 && partial.text === 'Hello. ', 'mid-segment interruption keeps only fully heard text');
check(response.context()?.interrupted === true && !response.context()?.content.includes('promise'), 'rebuilt context excludes unheard promise');
check(!response.reportPlayed('s', 1, 1000), 'late completion cannot add text after interruption');
check(response.finish('completed') === partial, 'duplicate finalization preserves first terminal outcome');
const bilingual = new HeardResponse('s', 2, 1000, timed);
bilingual.reportPlayed('s', 2, 600);
check(bilingual.finish('interrupted').text === 'Hello. नमस्ते। ', 'exact boundary preserves complete Hindi text');
const whole = new HeardResponse('s', 3, 1000, timed);
whole.reportPlayed('s', 3, 1000);
check(whole.finish('completed').text === timed.map(s => s.text).join(''), 'full playout includes complete response');
check(whole.context()?.interrupted === false, 'completed context is distinguished from interrupted');
const unheard = new HeardResponse('s', 4, 1000, timed); unheard.finish('interrupted');
check(unheard.context() === undefined, 'entirely unheard response omitted from next context');
const unaligned = new HeardResponse('s', 5, 1000, []); unaligned.reportPlayed('s', 5, 700); unaligned.finish('interrupted');
check(unaligned.context() === undefined, 'missing alignment never invents a text prefix');
rejects(() => new HeardResponse('s', 1, 1000, [{ text: 'A', startMs: 0, endMs: 300 }, { text: 'B', startMs: 200, endMs: 400 }]), 'invalid_alignment');
rejects(() => new HeardResponse('s', 1, 1000, [{ text: 'क', startMs: 0, endMs: 100 }, { text: 'ि', startMs: 100, endMs: 200 }]), 'split_grapheme');
rejects(() => new HeardResponse('s', 1, 1000, [{ text: '👩', startMs: 0, endMs: 100 }, { text: '\u200d💻', startMs: 100, endMs: 200 }]), 'split_grapheme');
const copied = [{ text: 'Original', startMs: 0, endMs: 100 }];
const immutable = new HeardResponse('s', 6, 100, copied); copied[0].text = 'Changed';
immutable.reportPlayed('s', 6, 100); check(immutable.finish('completed').text === 'Original', 'input mutation cannot rewrite timing history');
// Exercise the same interruption against coordinator, worker and fake transport.
const scope = { account: 'synthetic', companion: 'synthetic' };
const sessions = new LocalVoiceSessions({ leaseMs: 1000, durationMs: 5000, capacity: 2 }, () => 0);
const fake = new FakeVoiceTransport(), worker = new LocalVoiceWorker(sessions, fake);
const session = sessions.open(scope, 'request'); const generation = sessions.beginResponse(scope, session.id);
await worker.pump(); fake.enqueue(session.id, generation.epoch, 0);
const tracked = new HeardResponse(session.id, generation.epoch, 1000, timed);
tracked.reportPlayed(session.id, generation.epoch, 600);
// Capture final local playout position before advancing/flushing its epoch.
tracked.finish('interrupted'); sessions.interrupt(scope, session.id); await worker.pump();
check(fake.queued(session.id) === 0 && tracked.context()?.content === 'Hello. नमस्ते। ', 'interruption clears fake media and retains only heard bilingual context');
check(!tracked.reportPlayed(session.id, generation.epoch, 1000) &&
  !sessions.acceptFrame(scope, session.id, generation.epoch, 1), 'late media and late history reports both rejected');
console.log(`PASS ${count} heard-response checks; synthetic alignment/playout only, not acoustic or Hindi voice quality evidence`);
