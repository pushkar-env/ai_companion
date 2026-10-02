// Local Windows Editor prototype. No cloud calls, transcript files or production auth.
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { randomBytes, timingSafeEqual } from 'node:crypto';
import { mkdir, writeFile, rm } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
const sessionPath = resolve(root, 'artifacts/talking-character/session.json');
const model = process.env.COMPANION_LOCAL_MODEL || 'qwen2.5:7b';
const token = randomBytes(32).toString('hex');
const emotions = ['neutral', 'happy', 'concerned', 'curious'];
let busy = false, authority;
function speak(text, signal) {
  return new Promise((resolveSpeech, reject) => {
    const child = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-File', resolve(root, 'tools/speak-local.ps1')], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    let output = '', size = 0;
    const abort = () => { child.kill(); reject(new Error('cancelled')); };
    signal.addEventListener('abort', abort, { once: true });
    child.stdout.on('data', chunk => { size += chunk.length; if (size > 6_000_000) abort(); else output += chunk; });
    child.stderr.resume();
    child.on('error', () => reject(new Error('speech_unavailable')));
    child.on('close', code => {
      signal.removeEventListener('abort', abort);
      if (code !== 0) return reject(new Error('speech_unavailable'));
      try { resolveSpeech(JSON.parse(output.replace(/^\uFEFF/, ''))); } catch { reject(new Error('speech_unavailable')); }
    });
    child.stdin.on('error', () => {});
    child.stdin.end(JSON.stringify({ text }));
    if (signal.aborted) abort();
  });
}
const server = createServer(async (req, res) => {
  const send = (status, body) => { if (!res.destroyed && !res.writableEnded) { res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'Connection': 'close' }); res.end(JSON.stringify(body)); } };
  if (req.headers.host !== authority || req.headers.origin !== undefined) { req.resume(); return send(403, { error: 'local_only' }); }
  const supplied = req.headers.authorization || '';
  const expected = `Bearer ${token}`;
  const suppliedBytes = Buffer.from(supplied), expectedBytes = Buffer.from(expected);
  if (suppliedBytes.length !== expectedBytes.length || !timingSafeEqual(suppliedBytes, expectedBytes)) { req.resume(); return send(401, { error: 'unauthorized' }); }
  if (req.method === 'GET' && req.url === '/health') return send(200, { mode: 'local-ai', model, voice: 'Microsoft Zira Desktop' });
  if (req.method !== 'POST' || req.url !== '/turn') { req.resume(); return send(404, { error: 'not_found' }); }
  if (busy) { req.resume(); return send(429, { error: 'busy_retry' }); }
  if (req.headers['content-type'] !== 'application/json') { req.resume(); return send(415, { error: 'json_required' }); }
  busy = true;
  const controller = new AbortController();
  const deadline = setTimeout(() => controller.abort(), 90000);
  res.on('close', () => { if (!res.writableEnded) controller.abort(); });
  try {
    const parts = []; let size = 0;
    for await (const chunk of req) { size += chunk.length; if (size > 16000) { send(413, { error: 'too_large' }); return; } parts.push(chunk); }
    let input; try { input = JSON.parse(Buffer.concat(parts).toString('utf8')); } catch { send(400, { error: 'invalid_json' }); return; }
    if (typeof input?.message !== 'string' || !input.message.trim() || input.message.length > 500 || !Array.isArray(input.history) || input.history.length > 8 || input.history.some(m => !['user','assistant'].includes(m.role) || typeof m.content !== 'string' || m.content.length > 600)) { send(400, { error: 'invalid_input' }); return; }
    const system = 'You are a friendly adult AI companion in a local test. Be warm, concise, non-explicit, and make no clinical claims. Never pretend to be human. Respond in English for this English speech prototype. Answer the latest message naturally in at most two short sentences and 55 words. Return only a JSON object with text and emotion. emotion must be neutral, happy, concerned, or curious and should match the reply. No markdown or stage directions.';
    const ai = await fetch('http://127.0.0.1:11434/api/chat', { method: 'POST', headers: { 'Content-Type': 'application/json' }, signal: controller.signal,
      body: JSON.stringify({ model, stream: false, format: 'json', keep_alive: '10m', options: { temperature: 0.65, num_predict: 180, num_ctx: 4096 }, messages: [{ role: 'system', content: system }, ...input.history, { role: 'user', content: input.message }] }) });
    if (!ai.ok) throw new Error('local_model_unavailable');
    const data = await ai.json(); let reply;
    try { reply = JSON.parse(data.message.content); } catch { throw new Error('invalid_model_reply'); }
    if (typeof reply.text !== 'string' || !reply.text.trim() || reply.text.length > 600) throw new Error('invalid_model_reply');
    const speech = await speak(reply.text, controller.signal);
    send(200, { text: reply.text, emotion: emotions.includes(reply.emotion) ? reply.emotion : 'neutral', ...speech });
  } catch (error) {
    send(503, { error: controller.signal.aborted ? 'cancelled_or_timeout' : ['speech_unavailable','invalid_model_reply','local_model_unavailable'].includes(error.message) ? error.message : 'local_service_unavailable' });
  } finally { clearTimeout(deadline); busy = false; }
});
server.requestTimeout = 100000; server.headersTimeout = 5000;
server.setTimeout(100000, socket => socket.destroy());
await new Promise(resolveListen => server.listen(0, '127.0.0.1', resolveListen));
authority = `127.0.0.1:${server.address().port}`;
await mkdir(resolve(root, 'artifacts/talking-character'), { recursive: true });
await writeFile(sessionPath, JSON.stringify({ url: `http://${authority}`, token, model, pid: process.pid }), { mode: 0o600 });
console.log('Local talking-character service ready. Session configuration is in ignored artifacts; no transcript logging.');
async function shutdown() { await rm(sessionPath, { force: true }); server.closeAllConnections(); server.close(); }
process.once('SIGINT', shutdown); process.once('SIGTERM', shutdown);
