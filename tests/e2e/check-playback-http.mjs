import { startLocalPlaybackTest } from '../../services/voice-agent/local-http.mjs';
import { request } from 'node:http';
let checks = 0, now = 1000;
function check(ok, label) { if (!ok) throw new Error(label); checks++; console.log('PASS ' + label); }
const fixture = { account: 'synthetic-a', session: 's-a', utterance: 'u-a', epoch: 1,
  sampleRate: 24000, durationSamples: 48000, deliveredSamples: 24000,
  segments: [{ text: 'Hello. ', startMs: 0, endMs: 500 }, { text: 'Unheard.', startMs: 600, endMs: 1800 }] };
const service = await startLocalPlaybackTest([fixture, { ...fixture, account: 'synthetic-b', session: 's-b' }], { now: () => now });
const report = { utteranceId: 'u-a', epoch: 1, terminal: 'interrupted', observedSamples: 12000, sampleRate: 24000, durationSamples: 48000 };
async function call({ token = service.credentials.get('synthetic-a'), session = 's-a', body = JSON.stringify(report), headers = {}, method = 'POST' } = {}) {
  return new Promise((resolve, reject) => {
    const req = request(`${service.url}/local/sessions/${session}/playback`, { method, agent: false,
      headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}`, ...headers } }, res => {
      const chunks = [];
      res.on('data', chunk => chunks.push(chunk));
      res.on('error', reject);
      res.on('end', () => resolve({ status: res.statusCode,
        headers: { get: name => res.headers[name] },
        json: async () => JSON.parse(Buffer.concat(chunks).toString('utf8')) }));
    });
    req.setTimeout(3000, () => req.destroy(new Error('test_request_timeout')));
    req.on('error', reject);
    req.end(method === 'POST' ? body : undefined);
  });
}
try {
  check(service.url.startsWith('http://127.0.0.1:'), 'service binds IPv4 loopback');
  check((await call({ token: 'invalid' })).status === 401, 'invalid token rejected');
  check((await call({ headers: { Origin: 'https://example.invalid' } })).status === 403, 'browser-origin request rejected');
  check((await call({ headers: { Host: 'example.invalid' } })).status === 403, 'foreign Host rejected');
  check((await call({ token: service.credentials.get('synthetic-b') })).status === 404, 'valid other-account token cannot access session');
  check((await call({ body: JSON.stringify({ ...report, account: 'synthetic-b' }) })).status === 400, 'body cannot override authenticated account');
  check((await call({ body: '{' })).status === 400, 'malformed JSON rejected');
  check((await call({ body: 'x'.repeat(4097) })).status === 413, 'oversized request rejected');
  check((await call({ headers: { 'Content-Type': 'text/plain' } })).status === 415, 'unexpected content type rejected');
  check((await call({ method: 'GET' })).status === 404, 'unsupported method rejected');
  check((await call({ body: JSON.stringify({ ...report, observedSamples: 24001 }) })).status === 422, 'HTTP claim cannot exceed registered delivery');
  const success = await call(); const first = await success.json();
  check(success.status === 200 && first.mode === 'synthetic-local' && first.receipt.text === 'Hello. ', 'HTTP receipt contains only heard synthetic text');
  check(success.headers.get('cache-control') === 'no-store', 'receipt response not cacheable');
  const duplicate = await call(); check(JSON.stringify(await duplicate.json()) === JSON.stringify(first), 'HTTP retry returns unchanged receipt');
  check((await call({ body: JSON.stringify({ ...report, observedSamples: 13000 }) })).status === 409, 'conflicting HTTP retry rejected');
  let bounded = true;
  for (let i = 0; i < 199; i++) {
    const response = await call({ token: service.credentials.get('synthetic-b'), session: 's-b' });
    bounded &&= response.status === 200;
  }
  check(bounded && (await call({ token: service.credentials.get('synthetic-b'), session: 's-b' })).status === 429,
    'fixture token request budget enforced');
  check((await call()).status === 200, 'other account request budget stays isolated');
  now += 300_000; check((await call()).status === 401, 'ephemeral local token expires');
} finally { await service.close(); }
check(service.credentials.size === 0, 'shutdown clears local credentials');
console.log(`PASS ${checks} actual loopback HTTP checks; synthetic token auth, no provider or production identity`);
