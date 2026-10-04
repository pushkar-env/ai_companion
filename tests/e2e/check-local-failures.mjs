import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
const s=JSON.parse(await readFile('artifacts/talking-character/session.json','utf8'));const lines=[];
async function post(body,token=s.token,signal){return fetch(s.url+'/turn-stream',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify(body),signal});}
async function errorFrame(response,status,code){assert.equal(response.status,status);assert.match(response.headers.get('content-type'),/application\/x-ndjson/);const raw=await response.text();assert.ok(raw.endsWith('\n'));const frame=JSON.parse(raw);assert.equal(frame.type,'error');assert.equal(frame.error,code);}
await errorFrame(await post({message:'hello',history:[]},'invalid'),401,'unauthorized');lines.push('PASS expired/invalid session returns framed unauthorized error');
await errorFrame(await post({message:'',history:[]}),400,'invalid_input');lines.push('PASS rejected stream input returns newline-terminated error frame');
const controller=new AbortController();
try {
 const first=await post({message:'Please say two short sentences about a cheerful morning.',history:[]},s.token,controller.signal);assert.equal(first.status,200);
 await errorFrame(await post({message:'Another turn',history:[]}),429,'busy_retry');lines.push('PASS concurrent turn receives framed busy/retry status');
} finally { controller.abort(); }
await mkdir('docs/evidence/m1/local-failures',{recursive:true});await writeFile('docs/evidence/m1/local-failures/service-checks.txt',lines.join('\n')+'\n');console.log(lines.join('\n'));
