import { LocalVoiceSessions } from '../../services/voice-agent/session-coordinator.ts';
import { FakeVoiceTransport, LocalVoiceWorker } from '../../services/voice-agent/local-worker.ts';
let assertions = 0;
function check(ok: boolean, label: string) { if (!ok) throw new Error(label); assertions++; console.log('PASS ' + label); }
let now = 0;
const scope = { account: 'synthetic', companion: 'companion' };
const sessions = new LocalVoiceSessions({ leaseMs: 100, durationMs: 1000, capacity: 5 }, () => now);
const fake = new FakeVoiceTransport(), worker = new LocalVoiceWorker(sessions, fake);
const first = sessions.open(scope, 'one');
const speaking = sessions.beginResponse(scope, first.id); await worker.pump();
check(fake.enqueue(first.id, speaking.epoch, 0), 'fake transport queues current epoch');
sessions.interrupt(scope, first.id);
const pending = sessions.pendingActions();
check(pending.length === sessions.pendingActions().length, 'reading outbox does not consume actions');
const cancel = pending.find(a => a.kind === 'cancel-generation')!;
fake.fail(cancel.id, 1);
check((await worker.pump()).length === 1, 'failed action remains retryable');
check(fake.queued(first.id) === 0, 'playback flush proceeds despite cancellation failure');
check(!fake.enqueue(first.id, speaking.epoch, 1), 'fake transport rejects interrupted epoch');
await worker.pump();
check(sessions.pendingActions().length === 0 && fake.effectCount(cancel.id) === 1, 'retry applies cancellation once and acknowledges');
const next = sessions.beginResponse(scope, first.id); await worker.pump();
check(fake.enqueue(first.id, next.epoch, 0), 'new response accepts fresh sequence');
// A delayed old flush is harmless even after later commands were applied.
await fake.apply({ id: 'delayed-flush', kind: 'flush-playback', session: first.id, epoch: speaking.epoch });
check(fake.queued(first.id) === 1, 'older delayed flush preserves newer queued frames');
sessions.end(scope, first.id, 'background');
const settle = sessions.pendingActions().find(a => a.kind === 'settle')!;
fake.fail(settle.id, 1, true);
await worker.pump();
check(fake.closed(first.id) && fake.queued(first.id) === 0, 'background stops transport and clears queue');
check(sessions.pendingActions().some(a => a.id === settle.id), 'lost settlement acknowledgement retains action');
// Restart dispatcher around the same retained coordinator and sink, not a process restart.
const recovered = new LocalVoiceWorker(sessions, fake); await recovered.pump();
check(fake.effectCount(settle.id) === 1 && sessions.pendingActions().length === 0, 'dispatcher recreation retries without duplicate settlement');
const orphan = sessions.open(scope, 'orphan'); sessions.beginResponse(scope, orphan.id); await recovered.pump();
now = 150; await recovered.pump();
check(fake.closed(orphan.id) && fake.settledMs(orphan.id) === 100, 'worker poll expires orphan and applies stop/settlement');
const failing = sessions.open(scope, 'failing'); sessions.end(scope, failing.id, 'user');
const stop = sessions.pendingActions().find(a => a.kind === 'stop-session')!;
fake.fail(stop.id, 3);
await recovered.pump(); await recovered.pump(); await recovered.pump();
check(recovered.failures()[0]?.state === 'blocked', 'retry exhaustion becomes observable blocked work');
check(fake.settledMs(failing.id) === undefined, 'failed stop holds dependent settlement');
await recovered.pump();
check(recovered.failures()[0]?.attempts === 3, 'blocked action does not retry forever');
recovered.retryBlocked(stop.id); await recovered.pump();
check(fake.closed(failing.id) && fake.settledMs(failing.id) === 0, 'explicit recovery applies stop then settlement');
check(recovered.failures().length === 0 && sessions.pendingActions().length === 0, 'recovery clears pending work');
const bounded = sessions.open(scope, 'bounded'); const epoch = sessions.beginResponse(scope, bounded.id).epoch; await recovered.pump();
let queued = true; for (let i = 0; i < 64; i++) queued = fake.enqueue(bounded.id, epoch, i) && queued;
check(queued && fake.queued(bounded.id) === 64, 'fake buffer accepts configured capacity');
check(!fake.enqueue(bounded.id, epoch, 64), 'fake media buffer enforces capacity');
sessions.interrupt(scope, bounded.id);
let aborted = false;
const hanging = new LocalVoiceWorker(sessions, { async apply(action, signal) {
  if (action.kind === 'cancel-generation') {
    signal.addEventListener('abort', () => { aborted = true; }, { once: true });
    await new Promise<void>(() => {});
  } else await fake.apply(action, signal);
} }, 1, 5);
const pumping = hanging.pump();
let busy = false; try { await hanging.pump(); } catch (e) { busy = (e as Error).message === 'worker_busy'; }
check(busy, 'overlapping pumps rejected');
await pumping;
check(aborted && hanging.failures()[0]?.state === 'blocked', 'hung adapter times out and receives abort');
check(fake.queued(bounded.id) === 0, 'timeout does not prevent later playback flush');
console.log(`PASS ${assertions} offline worker/transport assertions; no real audio, provider, billing or process-crash recovery`);
