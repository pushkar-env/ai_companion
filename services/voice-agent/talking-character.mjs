// Local Windows Editor prototype. No cloud calls, transcript files or production auth.
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { randomBytes, timingSafeEqual } from 'node:crypto';
import { mkdir, writeFile, rm } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { speechChunks } from './speech-chunks.mjs';
import { existsSync } from 'node:fs';
import { checkLocalAi } from './local-readiness.mjs';
const root = fileURLToPath(new URL('../../', import.meta.url));
const sessionPath = resolve(root, 'artifacts/talking-character/session.json');
const model = process.env.COMPANION_LOCAL_MODEL || 'qwen2.5:7b';
const token = randomBytes(32).toString('hex');
const emotions = ['neutral', 'happy', 'concerned', 'curious'];
const whisperPython = resolve(root, 'artifacts/whisper-env/Scripts/python.exe');
const useWhisper = existsSync(whisperPython) && existsSync(resolve(root, 'artifacts/whisper-small/model.bin'));
let busy = false, authority;
let readinessPending;
function speechTask(script, input, signal) {
  return new Promise((resolveSpeech, reject) => {
    const whisper = script === 'transcribe-whisper.py';
    const child = spawn(whisper ? whisperPython : 'powershell.exe', whisper ? [resolve(root, 'tools', script)] : ['-NoProfile', '-NonInteractive', '-File', resolve(root, 'tools', script)], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
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
    child.stdin.end(JSON.stringify(input));
    if (signal.aborted) abort();
  });
}
const server = createServer(async (req, res) => {
  const streaming = req.url === '/turn-stream';
  const send = (status, body) => { if (!res.destroyed && !res.writableEnded) { const frame = streaming && status >= 400; res.writeHead(status, { 'Content-Type': frame ? 'application/x-ndjson' : 'application/json', 'Cache-Control': 'no-store', 'Connection': 'close' }); res.end(JSON.stringify(frame ? {type:'error',...body} : body) + (frame ? '\n' : '')); } };
  if (req.headers.host !== authority || req.headers.origin !== undefined) { req.resume(); return send(403, { error: 'local_only' }); }
  const supplied = req.headers.authorization || '';
  const expected = `Bearer ${token}`;
  const suppliedBytes = Buffer.from(supplied), expectedBytes = Buffer.from(expected);
  if (suppliedBytes.length !== expectedBytes.length || !timingSafeEqual(suppliedBytes, expectedBytes)) { req.resume(); return send(401, { error: 'unauthorized' }); }
  if (req.method === 'GET' && req.url === '/health') return send(200, { mode: 'local-ai', model, voice: 'Microsoft Zira Desktop' });
  if (req.method === 'GET' && req.url === '/readiness') {
    // Share concurrent probes; this never occupies the conversation turn slot.
    readinessPending ??= checkLocalAi(model).finally(() => { readinessPending = null; });
    return send(200, { ai: await readinessPending, model,
      transcription: useWhisper ? 'whisper_configured' : 'windows_configured',
      speech: 'not_tested' });
  }
  const transcribe = req.url === '/transcribe';
  if (req.method !== 'POST' || (!transcribe && !streaming && req.url !== '/turn')) { req.resume(); return send(404, { error: 'not_found' }); }
  if (busy) { req.resume(); return send(429, { error: 'busy_retry' }); }
  if (req.headers['content-type'] !== 'application/json') { req.resume(); return send(415, { error: 'json_required' }); }
  busy = true;
  const controller = new AbortController();
  const deadline = setTimeout(() => controller.abort(), transcribe ? 30000 : 90000);
  res.on('close', () => { if (!res.writableEnded) controller.abort(); });
  try {
    const parts = []; let size = 0;
    for await (const chunk of req) { size += chunk.length; if (size > (transcribe ? 860000 : 16000)) { send(413, { error: 'too_large' }); return; } parts.push(chunk); }
    let input; try { input = JSON.parse(Buffer.concat(parts).toString('utf8')); } catch { send(400, { error: 'invalid_json' }); return; }
    if (transcribe) {
      if (input?.sampleRate !== 16000 || typeof input.pcm !== 'string' || input.pcm.length > 853336 || Object.keys(input).length !== 2) { send(400, { error: 'invalid_audio' }); return; }
      const pcm = Buffer.from(input.pcm, 'base64');
      if (pcm.length < 8000 || pcm.length > 640000 || pcm.length % 2 || pcm.toString('base64') !== input.pcm) { send(400, { error: 'invalid_audio' }); return; }
      const result = await speechTask(useWhisper ? 'transcribe-whisper.py' : 'transcribe-local.ps1', input, controller.signal);
      if (typeof result.text !== 'string' || result.text.length > 500) throw new Error('invalid_transcript');
      send(200, { text: result.text, confidence: result.confidence, warning: result.warning || (result.confidence < .55 && result.text ? 'uncertain' : ''), mode: useWhisper ? 'local-whisper-english' : 'local-windows-english', reviewRequired: true });
      return;
    }
    if (typeof input?.message !== 'string' || !input.message.trim() || input.message.length > 500 || !Array.isArray(input.history) || input.history.length > 8 || input.history.some(m => !m || !['user','assistant'].includes(m.role) || typeof m.content !== 'string' || m.content.length > 600)) { send(400, { error: 'invalid_input' }); return; }
    const system = 'You are a friendly adult AI companion in a local test. Be warm, concise, non-explicit, and make no clinical claims. Never pretend to be human. Respond in English for this English speech prototype. Answer the latest message naturally in at most two short sentences and 55 words. Return only a JSON object with text and emotion. emotion must be neutral, happy, concerned, or curious and should match the reply. No markdown or stage directions.';
    const ai = await fetch('http://127.0.0.1:11434/api/chat', { method: 'POST', headers: { 'Content-Type': 'application/json' }, signal: controller.signal,
      body: JSON.stringify({ model, stream: false, format: { type: 'object', properties: { text: { type: 'string' }, emotion: { type: 'string', enum: emotions } }, required: ['text', 'emotion'], additionalProperties: false }, keep_alive: '10m', options: { temperature: 0.65, num_predict: 180, num_ctx: 4096 }, messages: [{ role: 'system', content: system }, ...input.history, { role: 'user', content: input.message }] }) }).catch(() => { throw new Error('local_model_unavailable'); });
    if (!ai.ok) throw new Error('local_model_unavailable');
    const data = await ai.json(); let reply;
    try { reply = JSON.parse(data.message.content); } catch { throw new Error('invalid_model_reply'); }
    if (typeof reply.text !== 'string' || !reply.text.trim() || reply.text.length > 600) throw new Error('invalid_model_reply');
    const emotion = emotions.includes(reply.emotion) ? reply.emotion : 'neutral';
    if (streaming) {
      res.writeHead(200, { 'Content-Type': 'application/x-ndjson', 'Cache-Control': 'no-store', 'Connection': 'close' });
      res.write(JSON.stringify({ type: 'text', text: reply.text, emotion }) + '\n');
      let sequence = 0;
      for (const text of speechChunks(reply.text)) {
        if (controller.signal.aborted || res.destroyed) throw new Error('cancelled');
        const speech = await speechTask('speak-local.ps1', { text }, controller.signal);
        if (controller.signal.aborted || res.destroyed) throw new Error('cancelled');
        res.write(JSON.stringify({ type: 'audio', sequence: sequence++, text, emotion, ...speech }) + '\n');
      }
      res.end(JSON.stringify({ type: 'done', sequence }) + '\n');
      return;
    }
    const speech = await speechTask('speak-local.ps1', { text: reply.text }, controller.signal);
    send(200, { text: reply.text, emotion, ...speech });
  } catch (error) {
    const code = controller.signal.aborted ? 'cancelled_or_timeout' : ['speech_unavailable','invalid_model_reply','local_model_unavailable'].includes(error.message) ? error.message : 'local_service_unavailable';
    if (res.headersSent) { if (!res.destroyed) res.end(JSON.stringify({ type: 'error', error: code }) + '\n'); return; }
    send(503, { error: code });
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
