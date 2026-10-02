import { HeardResponse } from './heard-response.ts';
import type { TimedText } from './heard-response.ts';

// Offline bridge only: caller supplies the expected session/utterance/epoch and alignment.
// This validates structure, not the truth of a client's claim or its authorization.
export function observeUnityPlayback(session: string, utterance: string, epoch: number,
  input: unknown, segments: readonly TimedText[]): HeardResponse {
  if (typeof input !== 'object' || input === null) throw new Error('invalid_playback_report');
  const r = input as Record<string, unknown>;
  if (r.utteranceId !== utterance || r.epoch !== epoch || !utterance ||
      !Number.isSafeInteger(epoch) || epoch < 0 ||
      !['interrupted', 'source_stopped'].includes(String(r.terminal))) throw new Error('invalid_playback_identity');
  const samples = r.observedSamples, rate = r.sampleRate, duration = r.durationSamples;
  if (typeof samples !== 'number' || typeof rate !== 'number' || typeof duration !== 'number' ||
      !Number.isSafeInteger(samples) || !Number.isSafeInteger(rate) || !Number.isSafeInteger(duration) ||
      samples < 0 || samples > duration || duration <= 0 || rate < 8000 || rate > 192000 || duration > rate * 300)
    throw new Error('invalid_playback_samples');
  const tracked = new HeardResponse(session, epoch, Math.ceil(duration * 1000 / rate), segments);
  tracked.reportPlayed(session, epoch, Math.floor(samples * 1000 / rate));
  // Neither report kind certifies complete acoustic playout; keep the conservative flag.
  tracked.finish('interrupted'); return tracked;
}
