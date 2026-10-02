// Consume actual Editor-exported sample metadata, with explicitly synthetic text timing.
import { readFileSync } from 'node:fs';
import { LocalPlaybackReceipts } from '../../services/voice-agent/playback-receipts.ts';
const file = process.argv[2] ?? 'docs/evidence/m1/cc-character-test/playback-report.json';
const report = JSON.parse(readFileSync(file, 'utf8'));
const duration = Math.ceil(report.durationSamples * 1000 / report.sampleRate);
const observed = Math.floor(report.observedSamples * 1000 / report.sampleRate);
if (observed < 250 || observed >= duration) throw new Error('Expected the Editor partial-playback fixture');
const receipts = new LocalPlaybackReceipts(1);
receipts.register({ account: 'synthetic', session: 'offline-editor-fixture', utterance: report.utteranceId,
  epoch: report.epoch, sampleRate: report.sampleRate, durationSamples: report.durationSamples,
  deliveredSamples: report.durationSamples, segments: [
  { text: 'Synthetic cue A. ', startMs: 0, endMs: 250 },
  { text: 'Synthetic unplayed tail.', startMs: 250, endMs: duration }
] });
const receipt = receipts.accept('synthetic', 'offline-editor-fixture', report);
if (receipt.text !== 'Synthetic cue A. ') throw new Error('Unplayed tail entered context');
if (receipts.accept('synthetic', 'offline-editor-fixture', report) !== receipt) throw new Error('Retry changed receipt');
console.log(`PASS actual Unity report -> backend tracker: ${observed} ms observed; unplayed synthetic tail excluded`);
console.log('Text alignment is a test fixture. The Unity audio is a tone, not speech. No network connection.');
