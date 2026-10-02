// Local deterministic simulation only. No authentication, media, vendor calls or billing.
export type Scope = Readonly<{ account: string; companion: string }>;
export type EndReason = 'user' | 'background' | 'logout' | 'quota' | 'policy' | 'lease' | 'duration';
export type Snapshot = Readonly<{
  id: string; state: 'listening' | 'speaking' | 'ended'; epoch: number;
  leaseUntil: number; deadline: number; reason?: EndReason;
}>;
export type Action = Readonly<{
  id: string;
  kind: 'cancel-generation' | 'flush-playback' | 'stop-session' | 'settle';
  session: string; epoch: number; elapsedMs?: number; reason?: EndReason;
}>;
type Session = {
  scope: string; request: string; snapshot: Snapshot; started: number;
  sequence: number; generations: number;
};

export class LocalVoiceSessions {
  private sessions = new Map<string, Session>();
  private requests = new Map<string, string>();
  private actions: Action[] = [];
  private lastTime = -1;
  private serial = 0;
  private actionSerial = 0;
  private readonly leaseMs: number;
  private readonly durationMs: number;
  private readonly capacity: number;

  constructor(options: { leaseMs: number; durationMs: number; capacity: number }, privateNow: () => number) {
    for (const value of Object.values(options))
      if (!Number.isSafeInteger(value) || value <= 0) throw new Error('invalid_limits');
    this.leaseMs = options.leaseMs; this.durationMs = options.durationMs;
    this.capacity = options.capacity; this.now = privateNow;
  }
  private now: () => number;
  private time(): number {
    const value = this.now();
    if (!Number.isSafeInteger(value) || value < this.lastTime || value < 0 ||
        value > Number.MAX_SAFE_INTEGER - Math.max(this.leaseMs, this.durationMs))
      throw new Error('invalid_clock');
    this.lastTime = value; return value;
  }
  private scopeKey(scope: Scope): string {
    for (const value of [scope.account, scope.companion])
      if (typeof value !== 'string' || !value.trim() || value.length > 128) throw new Error('invalid_scope');
    return JSON.stringify([scope.account, scope.companion]);
  }
  private owned(scope: Scope, id: string): Session {
    const key = this.scopeKey(scope), session = this.sessions.get(id);
    if (!session || session.scope !== key) throw new Error('session_not_found');
    return session;
  }
  private emit(kind: Action['kind'], s: Session, rest: Partial<Action> = {}): void {
    this.actions.push(Object.freeze({ ...rest, id: 'mock-action-' + ++this.actionSerial, kind, session: s.snapshot.id, epoch: s.snapshot.epoch }));
  }
  private invalidate(s: Session): void {
    this.emit('cancel-generation', s); // Cancel the old epoch before announcing its replacement.
    s.snapshot = Object.freeze({ ...s.snapshot, epoch: s.snapshot.epoch + 1 });
    s.sequence = -1; this.emit('flush-playback', s);
  }
  private finish(s: Session, reason: EndReason, at: number): void {
    if (s.snapshot.state === 'ended') return;
    this.invalidate(s);
    s.snapshot = Object.freeze({ ...s.snapshot, state: 'ended', reason });
    this.emit('stop-session', s, { reason });
    this.emit('settle', s, { reason, elapsedMs: Math.max(0, at - s.started) });
  }
  // A future worker must schedule this independently of client heartbeats.
  tick(): void {
    const now = this.time();
    for (const s of this.sessions.values()) {
      const expiry = Math.min(s.snapshot.leaseUntil, s.snapshot.deadline);
      if (s.snapshot.state !== 'ended' && now >= expiry)
        this.finish(s, s.snapshot.deadline <= s.snapshot.leaseUntil ? 'duration' : 'lease', expiry);
    }
  }
  open(scope: Scope, request: string): Snapshot {
    const key = this.scopeKey(scope);
    if (typeof request !== 'string' || !request.trim() || request.length > 128) throw new Error('invalid_request');
    this.tick();
    const requestKey = JSON.stringify([key, request]);
    const prior = this.requests.get(requestKey);
    if (prior) return this.sessions.get(prior)!.snapshot; // Ended retries cannot restart work.
    if ([...this.sessions.values()].some(s => s.scope === key && s.snapshot.state !== 'ended'))
      throw new Error('active_session');
    if (this.sessions.size >= this.capacity) throw new Error('local_capacity');
    const now = this.lastTime, id = 'mock-session-' + ++this.serial;
    const snapshot: Snapshot = Object.freeze({ id, state: 'listening', epoch: 0,
      deadline: now + this.durationMs, leaseUntil: now + Math.min(this.leaseMs, this.durationMs) });
    this.sessions.set(id, { scope: key, request, snapshot, started: now, sequence: -1, generations: 0 });
    this.requests.set(requestKey, id); return snapshot;
  }
  heartbeat(scope: Scope, id: string): Snapshot {
    const s = this.owned(scope, id); this.tick();
    if (s.snapshot.state === 'ended') throw new Error('session_ended');
    s.snapshot = Object.freeze({ ...s.snapshot, leaseUntil: Math.min(this.lastTime + this.leaseMs, s.snapshot.deadline) });
    return s.snapshot;
  }
  beginResponse(scope: Scope, id: string): Snapshot {
    const s = this.owned(scope, id); this.tick();
    if (s.snapshot.state !== 'listening') throw new Error('not_listening');
    if (s.generations >= 100) throw new Error('local_generation_limit');
    s.generations++; this.invalidate(s);
    s.snapshot = Object.freeze({ ...s.snapshot, state: 'speaking' }); return s.snapshot;
  }
  acceptFrame(scope: Scope, id: string, epoch: number, sequence: number): boolean {
    const s = this.owned(scope, id); this.tick();
    if (s.snapshot.state !== 'speaking' || epoch !== s.snapshot.epoch ||
        !Number.isSafeInteger(sequence) || sequence < 0 || sequence <= s.sequence) return false;
    s.sequence = sequence; return true;
  }
  interrupt(scope: Scope, id: string): Snapshot {
    const s = this.owned(scope, id); this.tick();
    if (s.snapshot.state === 'speaking') {
      this.invalidate(s); s.snapshot = Object.freeze({ ...s.snapshot, state: 'listening' });
    }
    return s.snapshot;
  }
  complete(scope: Scope, id: string, epoch: number): Snapshot {
    const s = this.owned(scope, id); this.tick();
    if (s.snapshot.state === 'speaking' && epoch === s.snapshot.epoch) {
      this.invalidate(s); s.snapshot = Object.freeze({ ...s.snapshot, state: 'listening' });
    }
    return s.snapshot;
  }
  end(scope: Scope, id: string, reason: Exclude<EndReason, 'lease' | 'duration'>): Snapshot {
    if (!['user', 'background', 'logout', 'quota', 'policy'].includes(reason)) throw new Error('invalid_reason');
    const s = this.owned(scope, id); this.tick(); this.finish(s, reason, this.lastTime); return s.snapshot;
  }
  drainActions(): readonly Action[] { const result = this.actions; this.actions = []; return Object.freeze(result); }
  pendingActions(): readonly Action[] { return Object.freeze([...this.actions]); }
  acknowledge(id: string): void { this.actions = this.actions.filter(action => action.id !== id); }
}
