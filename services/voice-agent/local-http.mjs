// Synthetic loopback test service only. Never a production identity or media service.
import { createServer } from 'node:http';
import { randomBytes, createHash } from 'node:crypto';
import { LocalPlaybackReceipts } from './playback-receipts.ts';

export async function startLocalPlaybackTest(deliveries, { now = Date.now } = {}) {
  if (!Array.isArray(deliveries) || deliveries.length === 0 || deliveries.length > 100)
    throw new Error('invalid_fixture_count');
  const receipts = new LocalPlaybackReceipts(100), credentials = new Map(), grants = new Map();
  const hash = value => createHash('sha256').update(value).digest('hex');
  const expires = now() + 300_000;
  for (const delivery of deliveries) {
    receipts.register(delivery);
    if (!credentials.has(delivery.account)) {
      const token = randomBytes(32).toString('hex');
      credentials.set(delivery.account, token);
      grants.set(hash(token), { account: delivery.account, requests: 0 });
    }
  }
  let authority;
  const server = createServer(async (req, res) => {
    const send = (status, body) => {
      if (res.destroyed || res.writableEnded) return;
      res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store',
        'X-Content-Type-Options': 'nosniff', 'Connection': 'close' });
      res.end(JSON.stringify(body));
    };
    try {
      if (req.headers.host !== authority || req.headers.origin !== undefined) {
        req.resume(); return send(403, { code: 'local_origin_required' });
      }
      const bearer = req.headers.authorization;
      const token = typeof bearer === 'string' && /^Bearer [a-f0-9]{64}$/.test(bearer) ? bearer.slice(7) : '';
      const grant = grants.get(hash(token));
      if (!grant || now() >= expires) { req.resume(); return send(401, { code: 'unauthorized' }); }
      if (++grant.requests > 200) { req.resume(); return send(429, { code: 'fixture_request_limit' }); }
      const match = /^\/local\/sessions\/([a-zA-Z0-9_-]{1,128})\/playback$/.exec(req.url ?? '');
      if (req.method !== 'POST' || !match) { req.resume(); return send(404, { code: 'not_found' }); }
      if (req.headers['content-type'] !== 'application/json') { req.resume(); return send(415, { code: 'json_required' }); }
      const parts = []; let size = 0;
      for await (const chunk of req) {
        size += chunk.length;
        if (size > 4096) { req.resume(); send(413, { code: 'body_too_large' }); return; }
        parts.push(chunk);
      }
      let input;
      try { input = JSON.parse(Buffer.concat(parts).toString('utf8')); }
      catch { return send(400, { code: 'invalid_json' }); }
      const fields = ['utteranceId', 'epoch', 'terminal', 'observedSamples', 'sampleRate', 'durationSamples'];
      if (!input || typeof input !== 'object' || Array.isArray(input) || Object.keys(input).length !== fields.length ||
          !fields.every(key => Object.hasOwn(input, key))) return send(400, { code: 'invalid_report_shape' });
      try { send(200, { mode: 'synthetic-local', receipt: receipts.accept(grant.account, match[1], input) }); }
      catch (error) {
        const code = error instanceof Error ? error.message : '';
        if (code === 'delivery_not_found') return send(404, { code: 'not_found' });
        if (['receipt_conflict', 'stale_playback_report'].includes(code)) return send(409, { code });
        if (['delivery_mismatch', 'invalid_playback_report', 'invalid_playback_identity', 'invalid_playback_samples'].includes(code))
          return send(422, { code: 'invalid_playback_report' });
        send(500, { code: 'local_error' });
      }
    } catch { send(400, { code: 'request_failed' }); }
  });
  server.requestTimeout = 5000; server.headersTimeout = 5000; server.setTimeout(5000, socket => socket.destroy());
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => { server.off('error', reject); resolve(); });
  });
  authority = `127.0.0.1:${server.address().port}`;
  return {
    url: `http://${authority}`, credentials,
    async close() { grants.clear(); credentials.clear(); server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
  };
}
