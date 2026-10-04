import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { checkLocalAi } from '../../services/voice-agent/local-readiness.mjs';
const lines=[];
async function check(name,fn){await fn();lines.push('PASS '+name);}
const response=data=>async()=>({ok:true,json:async()=>data});
await check('installed selected model ready',async()=>assert.equal(await checkLocalAi('qwen2.5:7b',response({models:[{name:'qwen2.5:7b'}]})),'ready'));
await check('missing model distinguished from offline engine',async()=>assert.equal(await checkLocalAi('missing',response({models:[]})),'model_missing'));
await check('default latest tag is equivalent',async()=>assert.equal(await checkLocalAi('sample',response({models:[{name:'sample:latest'}]})),'ready'));
await check('offline engine produces actionable status',async()=>assert.equal(await checkLocalAi('sample',async()=>{throw new Error('offline');}),'engine_unavailable'));
await check('malformed engine response is not ready',async()=>assert.equal(await checkLocalAi('sample',response({error:'bad'})),'engine_unavailable'));
await check('engine HTTP failure is not ready',async()=>assert.equal(await checkLocalAi('sample',async()=>({ok:false})),'engine_unavailable'));
const session=JSON.parse(await readFile('artifacts/talking-character/session.json','utf8'));
const get=token=>fetch(session.url+'/readiness',{headers:{Authorization:'Bearer '+token}});
await check('readiness requires session authentication',async()=>assert.equal((await get('invalid')).status,401));
await check('real installed local engine and model ready',async()=>{const r=await get(session.token);assert.equal(r.status,200);const d=await r.json();assert.equal(d.ai,'ready');assert.match(d.transcription,/^(whisper|windows)_configured$/);assert.equal(d.speech,'not_tested');});
await mkdir('docs/evidence/m1/local-readiness',{recursive:true});await writeFile('docs/evidence/m1/local-readiness/service-checks.txt',lines.join('\n')+'\n');console.log(lines.join('\n'));
