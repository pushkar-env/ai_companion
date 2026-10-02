// Offline playout bookkeeping. Timing comes from a trusted adapter, never text-length estimates.
export type TimedText = Readonly<{ text: string; startMs: number; endMs: number }>;
export type HeardRecord = Readonly<{
  session: string; epoch: number; status: 'completed' | 'interrupted';
  heardMs: number; text: string;
}>;

export class HeardResponse {
  private session: string;
  private epoch: number;
  private durationMs: number;
  private segments: readonly TimedText[];
  private playedMs = 0;
  private terminal: HeardRecord | undefined;
  constructor(session: string, epoch: number, durationMs: number, segments: readonly TimedText[]) {
    if (!session.trim() || session.length > 128 || !Number.isSafeInteger(epoch) || epoch < 0 ||
        !Number.isSafeInteger(durationMs) || durationMs <= 0 || durationMs > 300_000 || segments.length > 2000)
      throw new Error('invalid_response');
    let end = 0, text = '';
    for (const segment of segments) {
      if (!segment.text || !Number.isSafeInteger(segment.startMs) || !Number.isSafeInteger(segment.endMs) ||
          segment.startMs < end || segment.endMs <= segment.startMs || segment.endMs > durationMs)
        throw new Error('invalid_alignment');
      end = segment.endMs; text += segment.text;
    }
    if (text.length > 16_000) throw new Error('alignment_capacity');
    // Reject timing boundaries that would split Hindi combining marks or emoji clusters.
    const boundaries = new Set<number>([0, text.length]);
    for (const part of new Intl.Segmenter('und', { granularity: 'grapheme' }).segment(text)) boundaries.add(part.index);
    let offset = 0;
    for (const segment of segments) {
      offset += segment.text.length;
      if (!boundaries.has(offset)) throw new Error('split_grapheme');
    }
    this.session = session; this.epoch = epoch; this.durationMs = durationMs;
    this.segments = Object.freeze(segments.map(s => Object.freeze({ ...s })));
  }
  // A provider-generation cursor or queued-audio duration is NOT a playout report.
  reportPlayed(session: string, epoch: number, playedMs: number): boolean {
    if (this.terminal || session !== this.session || epoch !== this.epoch) return false;
    if (!Number.isSafeInteger(playedMs) || playedMs < this.playedMs || playedMs > this.durationMs) return false;
    this.playedMs = playedMs; return true;
  }
  finish(status: HeardRecord['status']): HeardRecord {
    if (this.terminal) return this.terminal;
    if (status !== 'completed' && status !== 'interrupted') throw new Error('invalid_status');
    if (status === 'completed' && this.playedMs !== this.durationMs) throw new Error('playout_incomplete');
    const text = this.segments.filter(s => s.endMs <= this.playedMs).map(s => s.text).join('');
    this.terminal = Object.freeze({ session: this.session, epoch: this.epoch, status, heardMs: this.playedMs, text });
    this.segments = []; // Do not retain the unheard draft inside the terminal tracker.
    return this.terminal;
  }
  context(): Readonly<{ role: 'assistant'; content: string; interrupted: boolean }> | undefined {
    if (!this.terminal) throw new Error('response_not_final');
    if (!this.terminal.text.trim()) return undefined;
    return Object.freeze({ role: 'assistant', content: this.terminal.text,
      interrupted: this.terminal.status === 'interrupted' });
  }
}
