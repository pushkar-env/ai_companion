import { observeUnityPlayback } from '../../services/voice-agent/unity-playback.ts';
let count = 0;
function check(value: boolean, label: string) { if (!value) throw new Error(label); count++; console.log('PASS ' + label); }
const report = { utteranceId: 'tone', epoch: 4, terminal: 'interrupted', observedSamples: 12001, sampleRate: 24000, durationSamples: 48000 };
const segments = [{ text: 'Heard. ', startMs: 0, endMs: 500 }, { text: 'Unheard.', startMs: 501, endMs: 1800 }];
const observed = observeUnityPlayback('session', 'tone', 4, report, segments);
check(observed.context()?.content === 'Heard. ', 'sample count converted conservatively to milliseconds');
check(observed.context()?.interrupted === true, 'observed report does not certify full hearing');
check(observeUnityPlayback('session', 'tone', 4, { ...report, terminal: 'source_stopped' }, segments).context()?.content === 'Heard. ', 'source stop does not promote full clip duration');
for (const change of [{ epoch: 3 }, { utteranceId: 'other' }, { sampleRate: 0 }, { observedSamples: 48001 }, { observedSamples: -1 }, { observedSamples: NaN }, { terminal: 'completed' }]) {
  let rejected = false; try { observeUnityPlayback('session', 'tone', 4, { ...report, ...change }, segments); } catch { rejected = true; }
  check(rejected, 'reject invalid report ' + Object.keys(change)[0]);
}
console.log(`PASS ${count} Unity playback bridge checks; no network or authenticated playout claims`);
