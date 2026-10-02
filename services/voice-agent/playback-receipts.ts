import { observeUnityPlayback } from './unity-playback.ts';
import type { TimedText, HeardRecord } from './heard-response.ts';

export type DeliveryManifest = Readonly<{
  account: string; session: string; utterance: string; epoch: number;
  sampleRate: number; durationSamples: number; deliveredSamples: number;
  segments: readonly TimedText[];
}>;
type Entry = { manifest: DeliveryManifest; fingerprint?: string; receipt?: HeardRecord };

// Local, volatile boundary. Account identity must come from a future authenticated server.
export class LocalPlaybackReceipts {
  private entries = new Map<string, Entry>();
  private active = new Map<string, { key: string; epoch: number }>();
  private capacity: number;
  constructor(capacity: number) {
    if (!Number.isSafeInteger(capacity) || capacity <= 0) throw new Error('invalid_capacity');
    this.capacity = capacity;
  }
  private scope(account: string, session: string): string {
    if (![account, session].every(v => typeof v === 'string' && v.trim().length > 0 && v.length <= 128))
      throw new Error('invalid_scope');
    return JSON.stringify([account, session]);
  }
  register(manifest: DeliveryManifest): void {
    const scope = this.scope(manifest.account, manifest.session);
    if (typeof manifest.utterance !== 'string' || !manifest.utterance.trim() || manifest.utterance.length > 128)
      throw new Error('invalid_utterance');
    const key = JSON.stringify([scope, manifest.utterance]);
    if (this.entries.has(key)) throw new Error('delivery_already_registered');
    const current = this.active.get(scope);
    if (current && manifest.epoch <= current.epoch) throw new Error('stale_delivery');
    if (!Number.isSafeInteger(manifest.deliveredSamples) || manifest.deliveredSamples < 0 ||
        manifest.deliveredSamples > manifest.durationSamples) throw new Error('invalid_delivery_bound');
    // Reuse numeric/alignment validation without treating this as a real playback claim.
    observeUnityPlayback(manifest.session, manifest.utterance, manifest.epoch, {
      utteranceId: manifest.utterance, epoch: manifest.epoch, terminal: 'interrupted',
      observedSamples: 0, sampleRate: manifest.sampleRate, durationSamples: manifest.durationSamples
    }, manifest.segments);
    if (this.entries.size >= this.capacity) throw new Error('receipt_capacity');
    const copy = Object.freeze({ ...manifest, segments: Object.freeze(manifest.segments.map(s => Object.freeze({ ...s }))) });
    this.entries.set(key, { manifest: copy }); this.active.set(scope, { key, epoch: manifest.epoch });
  }
  accept(account: string, session: string, input: unknown): HeardRecord {
    const scope = this.scope(account, session);
    if (typeof input !== 'object' || input === null || Array.isArray(input)) throw new Error('invalid_playback_report');
    const report = input as Record<string, unknown>;
    if (typeof report.utteranceId !== 'string') throw new Error('delivery_not_found');
    const key = JSON.stringify([scope, report.utteranceId]), entry = this.entries.get(key);
    if (!entry) throw new Error('delivery_not_found');
    const m = entry.manifest;
    if (report.sampleRate !== m.sampleRate || report.durationSamples !== m.durationSamples ||
        typeof report.observedSamples !== 'number' || report.observedSamples > m.deliveredSamples)
      throw new Error('delivery_mismatch');
    const tracked = observeUnityPlayback(session, m.utterance, m.epoch, report, m.segments);
    const fingerprint = JSON.stringify([report.utteranceId, report.epoch, report.terminal,
      report.observedSamples, report.sampleRate, report.durationSamples]);
    if (entry.receipt) {
      if (entry.fingerprint !== fingerprint) throw new Error('receipt_conflict');
      return entry.receipt;
    }
    if (this.active.get(scope)?.key !== key) throw new Error('stale_playback_report');
    entry.receipt = tracked.finish('interrupted'); entry.fingerprint = fingerprint;
    entry.manifest = Object.freeze({ ...m, segments: [] }); // Final receipt replaces the draft alignment.
    return entry.receipt;
  }
}
