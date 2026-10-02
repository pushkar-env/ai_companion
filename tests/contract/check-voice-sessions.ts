import { LocalVoiceSessions } from '../../services/voice-agent/session-coordinator.ts';
const owner = { account: 'synthetic-a', companion: 'synthetic-companion' };
const other = { account: 'synthetic-b', companion: 'synthetic-companion' };
let count = 0;
function check(ok: boolean, label: string) { if (!ok) throw new Error(label); count++; console.log('PASS ' + label); }
function rejects(fn: () => unknown, message: string) {
  let error = ''; try { fn(); } catch (e) { error = (e as Error).message; }
  check(error === message, 'reject ' + message);
}
let now = 0;
const sessions = new LocalVoiceSessions({ leaseMs: 30_000, durationMs: 60_000, capacity: 20 }, () => now);
const first = sessions.open(owner, 'connect-1');
check(sessions.open(owner, 'connect-1').id === first.id, 'duplicate connect returns same session');
rejects(() => sessions.open(owner, 'connect-2'), 'active_session');
rejects(() => sessions.heartbeat(other, first.id), 'session_not_found');
rejects(() => sessions.end(other, first.id, 'user'), 'session_not_found');
const independent = sessions.open(other, 'connect-1');
check(independent.id !== first.id, 'request keys isolated between accounts');
const speaking = sessions.beginResponse(owner, first.id);
check(sessions.acceptFrame(owner, first.id, speaking.epoch, 0), 'current epoch accepts frame');
check(!sessions.acceptFrame(owner, first.id, speaking.epoch, 0), 'duplicate frame rejected');
check(!sessions.acceptFrame(owner, first.id, speaking.epoch, NaN), 'invalid sequence rejected');
const interrupted = sessions.interrupt(owner, first.id);
check(interrupted.epoch > speaking.epoch && interrupted.state === 'listening', 'interrupt advances epoch and returns to listening');
check(!sessions.acceptFrame(owner, first.id, speaking.epoch, 1), 'late interrupted audio rejected');
const restarted = sessions.beginResponse(owner, first.id);
check(sessions.complete(owner, first.id, speaking.epoch).state === 'speaking', 'stale completion cannot finish new response');
check(sessions.acceptFrame(owner, first.id, restarted.epoch, 0), 'fresh epoch resets frame sequence');
check(sessions.complete(owner, first.id, restarted.epoch).state === 'listening', 'current completion returns to listening');
check(!sessions.acceptFrame(owner, first.id, restarted.epoch, 1), 'audio after completion rejected');
const actions = sessions.drainActions();
check(actions.some(a => a.kind === 'cancel-generation' && a.epoch === speaking.epoch), 'worker cancellation targets old generation');
check(actions.some(a => a.kind === 'flush-playback' && a.epoch === interrupted.epoch), 'playback flush carries replacement epoch');
now = 29_000;
check(sessions.heartbeat(owner, first.id).leaseUntil === 59_000, 'heartbeat renews lease before expiry');
now = 30_000; sessions.tick();
check(sessions.open(other, 'connect-1').state === 'ended', 'orphan expires without client callback');
rejects(() => sessions.heartbeat(other, independent.id), 'session_ended');
check(sessions.open(other, 'connect-2').id !== independent.id, 'fresh authorization key can start after lease loss');
now = 58_000;
check(sessions.heartbeat(owner, first.id).leaseUntil === 60_000, 'heartbeat cannot extend absolute session cap');
now = 61_000; sessions.tick(); sessions.tick(); sessions.end(owner, first.id, 'user');
const settlements = sessions.drainActions().filter(a => a.kind === 'settle');
check(settlements.filter(a => a.session === first.id).length === 1, 'expiry and repeated end settle once');
check(settlements.find(a => a.session === first.id)?.elapsedMs === 60_000, 'late sweep uses deadline rather than delayed observation');
check(sessions.open(owner, 'connect-1').reason === 'duration', 'old request remains terminal after retry');
for (const reason of ['background', 'logout', 'quota', 'policy', 'user'] as const) {
  const s = sessions.open(owner, reason); sessions.beginResponse(owner, s.id);
  const ended = sessions.end(owner, s.id, reason);
  check(ended.state === 'ended' && ended.reason === reason && !sessions.acceptFrame(owner, s.id, ended.epoch, 0), reason + ' closes session and rejects media');
  sessions.end(owner, s.id, reason);
  check(sessions.drainActions().filter(a => a.kind === 'settle').length === 1, reason + ' settles once');
}
now--; rejects(() => sessions.tick(), 'invalid_clock'); now++;
rejects(() => new LocalVoiceSessions({ leaseMs: 0, durationMs: 1, capacity: 1 }, () => 0), 'invalid_limits');
const bounded = new LocalVoiceSessions({ leaseMs: 1, durationMs: 1, capacity: 1 }, () => now);
bounded.open(owner, 'one'); now += 2; bounded.tick();
rejects(() => bounded.open(owner, 'two'), 'local_capacity');
const boundary = new LocalVoiceSessions({ leaseMs: 10, durationMs: 100, capacity: 2 }, () => now);
const edge = boundary.open(owner, 'edge'); now += 10;
rejects(() => boundary.heartbeat(owner, edge.id), 'session_ended');
check(boundary.drainActions().filter(a => a.kind === 'settle')[0]?.elapsedMs === 10, 'exact lease boundary expires before renewal');
check(boundary.drainActions().length === 0, 'drained actions are not redelivered in local simulator');
rejects(() => boundary.open({ account: '', companion: 'a' }, 'x'), 'invalid_scope');
console.log(`PASS ${count} local voice-session assertions; synthetic only, no network/media/provider billing`);
