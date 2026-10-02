import { LocalPlaybackReceipts } from '../../services/voice-agent/playback-receipts.ts';
let count = 0;
function check(ok: boolean, label: string) { if (!ok) throw new Error(label); count++; console.log('PASS ' + label); }
function rejects(fn: () => unknown, code: string) {
  let got = ''; try { fn(); } catch (e) { got = (e as Error).message; }
  check(got === code, 'reject ' + code);
}
const ledger = new LocalPlaybackReceipts(6);
const manifest = { account: 'a', session: 's', utterance: 'u', epoch: 1, sampleRate: 24000,
  durationSamples: 48000, deliveredSamples: 24000,
  segments: [{ text: 'Hello. ', startMs: 0, endMs: 500 }, { text: 'Unplayed.', startMs: 600, endMs: 1900 }] };
const report = { utteranceId: 'u', epoch: 1, sampleRate: 24000, durationSamples: 48000, observedSamples: 12000, terminal: 'interrupted' };
ledger.register(manifest);
rejects(() => ledger.accept('other', 's', report), 'delivery_not_found');
rejects(() => ledger.accept('a', 'other', report), 'delivery_not_found');
rejects(() => ledger.accept('a', 's', { ...report, sampleRate: 12000 }), 'delivery_mismatch');
rejects(() => ledger.accept('a', 's', { ...report, durationSamples: 96000 }), 'delivery_mismatch');
rejects(() => ledger.accept('a', 's', { ...report, observedSamples: 24001 }), 'delivery_mismatch');
rejects(() => ledger.accept('a', 's', { ...report, epoch: 2 }), 'invalid_playback_identity');
rejects(() => ledger.accept('a', 's', { ...report, observedSamples: -1 }), 'invalid_playback_samples');
manifest.segments[0].text = 'Mutated'; manifest.deliveredSamples = 48000;
const receipt = ledger.accept('a', 's', report);
check(receipt.text === 'Hello. ' && receipt.heardMs === 500, 'trusted alignment copied and unheard tail excluded');
check(ledger.accept('a', 's', { ...report }) === receipt, 'identical retry returns same finalized receipt');
rejects(() => ledger.accept('a', 's', { ...report, observedSamples: 13000 }), 'receipt_conflict');
rejects(() => ledger.accept('a', 's', { ...report, observedSamples: 30000 }), 'delivery_mismatch');
rejects(() => ledger.register(manifest), 'delivery_already_registered');
ledger.register({ ...manifest, utterance: 'u2', epoch: 2 });
ledger.register({ ...manifest, utterance: 'u3', epoch: 3 });
rejects(() => ledger.accept('a', 's', { ...report, utteranceId: 'u2', epoch: 2 }), 'stale_playback_report');
check(ledger.accept('a', 's', report) === receipt, 'old finalized receipt remains replayable after newer generation');
rejects(() => ledger.register({ ...manifest, utterance: 'old', epoch: 2 }), 'stale_delivery');
ledger.register({ ...manifest, account: 'b' });
check(ledger.accept('b', 's', report).text === 'Mutated', 'same utterance ID is isolated across accounts');
rejects(() => ledger.register({ ...manifest, utterance: 'bad', epoch: 4, deliveredSamples: 48001 }), 'invalid_delivery_bound');
const bounded = new LocalPlaybackReceipts(1); bounded.register(manifest);
rejects(() => bounded.register({ ...manifest, utterance: 'next', epoch: 2 }), 'receipt_capacity');
check(bounded.accept('a', 's', report).heardMs === 500, 'capacity failure preserves existing receipt registration');
console.log(`PASS ${count} scoped playback receipt checks; local trusted identities, no authentication or persistence`);
