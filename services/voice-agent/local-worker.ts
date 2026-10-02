import { LocalVoiceSessions } from './session-coordinator.ts';
import type { Action } from './session-coordinator.ts';

// Adapters must deduplicate by action.id, including retries after a lost acknowledgement.
export interface LocalActionSink { apply(action: Action, signal: AbortSignal): Promise<void>; }
export type Delivery = Readonly<{ id: string; kind: Action['kind']; attempts: number; state: 'retry' | 'blocked' }>;

export class LocalVoiceWorker {
  private attempts = new Map<string, number>();
  private running = false;
  private sessions: LocalVoiceSessions;
  private sink: LocalActionSink;
  private limit: number;
  private timeoutMs: number;
  constructor(sessions: LocalVoiceSessions, sink: LocalActionSink, limit = 3, timeoutMs = 1000) {
    if (!Number.isSafeInteger(limit) || limit < 1 || limit > 10) throw new Error('invalid_retry_limit');
    if (!Number.isSafeInteger(timeoutMs) || timeoutMs < 1 || timeoutMs > 60_000) throw new Error('invalid_timeout');
    this.sessions = sessions; this.sink = sink; this.limit = limit; this.timeoutMs = timeoutMs;
  }
  // Explicit deterministic poll, not an autonomous production watchdog or retry timer.
  async pump(): Promise<readonly Delivery[]> {
    if (this.running) throw new Error('worker_busy');
    this.running = true;
    try {
      this.sessions.tick();
      for (const action of this.sessions.pendingActions()) {
        // Do not simulate settlement until session-stop has been acknowledged.
        if (action.kind === 'settle' && this.sessions.pendingActions().some(
          a => a.session === action.session && a.kind === 'stop-session')) continue;
        const attempts = this.attempts.get(action.id) ?? 0;
        if (attempts >= this.limit) continue;
        this.attempts.set(action.id, attempts + 1);
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout> | undefined;
        try {
          await Promise.race([
            this.sink.apply(action, controller.signal),
            new Promise<never>((_, reject) => { timer = setTimeout(() => {
              controller.abort(); reject(new Error('delivery_timeout'));
            }, this.timeoutMs); })
          ]);
          this.sessions.acknowledge(action.id); this.attempts.delete(action.id);
        } catch { /* Retain work, omit adapter errors which may contain credentials/payloads. */ }
        finally { clearTimeout(timer); }
        // A failed cancellation must not prevent a separate playback flush or session stop.
      }
      return this.failures();
    } finally { this.running = false; }
  }
  failures(): readonly Delivery[] {
    return Object.freeze(this.sessions.pendingActions().filter(a => this.attempts.has(a.id)).map(a => {
      const attempts = this.attempts.get(a.id)!;
      return Object.freeze({ id: a.id, kind: a.kind, attempts,
        state: attempts >= this.limit ? 'blocked' as const : 'retry' as const });
    }));
  }
  // Operator-directed retry within this offline simulator; no automatic infinite retry.
  retryBlocked(id: string): void {
    if ((this.attempts.get(id) ?? 0) < this.limit) throw new Error('action_not_blocked');
    this.attempts.delete(id);
  }
}

type FakeState = { epoch: number; closed: boolean; frames: number[]; cancelled: Set<number> };
export class FakeVoiceTransport implements LocalActionSink {
  private seen = new Set<string>();
  private states = new Map<string, FakeState>();
  private settlements = new Map<string, number>();
  private faults = new Map<string, { remaining: number; after: boolean }>();
  private effects = new Map<string, number>();
  private state(id: string): FakeState {
    let s = this.states.get(id);
    if (!s) { s = { epoch: 0, closed: false, frames: [], cancelled: new Set() }; this.states.set(id, s); }
    return s;
  }
  fail(id: string, count: number, afterApply = false): void {
    if (!Number.isSafeInteger(count) || count < 1) throw new Error('invalid_fault_count');
    this.faults.set(id, { remaining: count, after: afterApply });
  }
  async apply(action: Action, signal?: AbortSignal): Promise<void> {
    signal?.throwIfAborted();
    const fault = this.faults.get(action.id);
    const failing = fault !== undefined && fault.remaining > 0;
    if (failing) fault.remaining--;
    if (failing && !fault.after) throw new Error('synthetic_transport_failure');
    if (!this.seen.has(action.id)) {
      const s = this.state(action.session);
      switch (action.kind) {
        case 'cancel-generation': s.cancelled.add(action.epoch); break;
        case 'flush-playback':
          // Delayed old flushes cannot erase a newer epoch's queued frames.
          if (action.epoch > s.epoch) { s.epoch = action.epoch; s.frames = []; }
          break;
        case 'stop-session': s.closed = true; s.frames = []; break;
        case 'settle':
          if (!s.closed) throw new Error('stop_required');
          this.settlements.set(action.session, action.elapsedMs ?? 0); break;
      }
      this.seen.add(action.id); this.effects.set(action.id, (this.effects.get(action.id) ?? 0) + 1);
    }
    if (failing && fault.after) throw new Error('synthetic_ack_lost');
  }
  enqueue(session: string, epoch: number, sequence: number): boolean {
    const s = this.state(session);
    if (s.closed || epoch !== s.epoch || s.cancelled.has(epoch) || !Number.isSafeInteger(sequence) ||
        sequence < 0 || (s.frames.length > 0 && sequence <= s.frames[s.frames.length - 1]) || s.frames.length >= 64) return false;
    s.frames.push(sequence); return true;
  }
  queued(session: string): number { return this.state(session).frames.length; }
  closed(session: string): boolean { return this.state(session).closed; }
  settledMs(session: string): number | undefined { return this.settlements.get(session); }
  effectCount(id: string): number { return this.effects.get(id) ?? 0; }
}
