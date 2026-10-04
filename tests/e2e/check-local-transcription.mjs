import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
const session=JSON.parse(await readFile(new URL('../../artifacts/talking-character/session.json',import.meta.url),'utf8'));
let count=0;
function check(ok,label){if(!ok)throw new Error(label);count++;console.log('PASS '+label);}
async function post(body,token=session.token){return fetch(session.url+'/transcribe',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify(body),signal:AbortSignal.timeout(35000)});}
const audio=await new Promise((resolve,reject)=>{
  const child=spawn('powershell.exe',['-NoProfile','-NonInteractive','-File','tools/speak-local.ps1'],{windowsHide:true,stdio:['pipe','pipe','pipe']});let output='';
  child.stdout.on('data',c=>output+=c);child.stderr.resume();child.on('error',reject);child.on('close',code=>{if(code!==0)reject(new Error('fixture synthesis failed'));else resolve(JSON.parse(output.replace(/^\uFEFF/,'')));});child.stdin.end(JSON.stringify({text:'Please tell me a short story about the sunshine.'}));
});
const fixture={pcm:audio.pcm,sampleRate:audio.sampleRate};
check((await post(fixture,'invalid')).status===401,'transcription requires local session token');
check((await post({...fixture,sampleRate:48000})).status===400,'unexpected sample rate rejected');
check((await post({...fixture,pcm:'not-base64'})).status===400,'invalid base64 rejected');
check((await post({...fixture,pcm:Buffer.alloc(100).toString('base64')})).status===400,'too-short audio rejected');
check((await post({...fixture,pcm:Buffer.alloc(640002).toString('base64')})).status===400,'over-20-second audio rejected');
check((await post({...fixture,account:'override'})).status===400,'unexpected audio fields rejected');
const silence=await post({pcm:Buffer.alloc(32000).toString('base64'),sampleRate:16000});
check(silence.status===200&&(await silence.json()).text==='','silence produces no invented transcript');
const spoken=await post(fixture);const result=await spoken.json();
check(spoken.status===200&&result.text.length>0,'generated English speech is transcribed locally');
check(result.reviewRequired===true&&['local-windows-english','local-whisper-english'].includes(result.mode),'transcript is explicitly marked for review');
check(result.confidence>=0&&result.confidence<=1,'recognizer returns bounded confidence');
await mkdir(new URL('../../artifacts/voice-input/',import.meta.url),{recursive:true});
await writeFile(new URL('../../artifacts/voice-input/synthetic-speech.json',import.meta.url),JSON.stringify(fixture));
console.log(`PASS ${count} transcription checks; generated audio only, no microphone recording`);
console.log('Generated fixture transcript: '+result.text);
