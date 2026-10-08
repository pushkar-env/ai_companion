import { readFile } from 'node:fs/promises';
import { speechChunks } from '../../services/voice-agent/speech-chunks.mjs';
const session=JSON.parse(await readFile(new URL('../../artifacts/talking-character/session.json',import.meta.url),'utf8'));
let count=0;function check(ok,label){if(!ok)throw new Error(label);count++;console.log('PASS '+label);}
check(speechChunks('One. Two. Three. Four.').length===3,'speech work is bounded to three chunks');
check(speechChunks('The price is 3.14. Good news!')[0]==='The price is 3.14.','decimal does not create a broken sentence');
check(speechChunks('Hello 🌞. Welcome!').join(' ')==='Hello 🌞. Welcome!','Unicode preserved at sentence boundaries');
async function open(body,signal){return fetch(session.url+'/turn-stream',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+session.token},body:JSON.stringify(body),signal});}
const body={message:'Please say exactly these two sentences: Welcome to a bright new day. We can take a small happy step together.',history:[]};
const start=performance.now();const response=await open(body,AbortSignal.timeout(95000));
check(response.ok&&response.headers.get('content-type')==='application/x-ndjson','real endpoint returns incremental NDJSON');
const events=[];const times=[];let pending='';const decoder=new TextDecoder();
for await(const chunk of response.body){pending+=decoder.decode(chunk,{stream:true});let newline;while((newline=pending.indexOf('\n'))>=0){events.push(JSON.parse(pending.slice(0,newline)));times.push(performance.now()-start);pending=pending.slice(newline+1);}}
check(pending===''&&events[0].type==='delta'&&events.at(-1).type==='done','text deltas precede audio and explicit completion terminates stream');
const deltas=events.filter(e=>e.type==='delta');
check(deltas.length>1&&deltas.every((e,i)=>e.sequence===i),'model emits multiple ordered text deltas');
check(deltas.map(e=>e.text).join('')===events.find(e=>e.type==='text').text,'real model deltas equal canonical final response');
const audio=events.filter(e=>e.type==='audio');
check(audio.length>=2&&audio.length<=3,'real two-sentence reply is delivered in separate clips');
check(audio.every((e,i)=>e.sequence===i)&&events.at(-1).sequence===audio.length,'audio sequence and final count match');
check(audio.every(e=>e.sampleRate===16000&&e.cues.length>0&&Buffer.from(e.pcm,'base64').length>8000),'every clip contains actual PCM and timed mouth cues');
const first=times[events.findIndex(e=>e.type==='audio')],last=times.at(-1);
check(first<last,'first audio arrives before final synthesis completes');
console.log(`Measured local request: first text ${Math.round(times[0])} ms; first audio ${Math.round(first)} ms; stream complete ${Math.round(last)} ms; head start ${Math.round(last-first)} ms`);
const controller=new AbortController();const cancelled=await open(body,controller.signal);let sawAudio=false;pending='';
try{for await(const chunk of cancelled.body){pending+=decoder.decode(chunk,{stream:true});if(pending.includes('"type":"audio"')){sawAudio=true;controller.abort();break;}}}catch(error){if(error.name!=='AbortError')throw error;}
check(sawAudio,'cancellation exercised after actual audio started arriving');
let released=false;for(let i=0;i<30&&!released;i++){await new Promise(r=>setTimeout(r,100));const probe=await open({message:'',history:[]},AbortSignal.timeout(3000));released=probe.status===400;await probe.text();}
check(released,'aborted stream releases the local service turn slot');
console.log(`PASS ${count} sentence-stream checks; local installed model and Windows speech`);
