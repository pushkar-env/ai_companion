import assert from 'node:assert/strict';
import {ReplyDecoder,streamReply} from '../../services/voice-agent/streamed-reply.mjs';
let checks=0;function check(ok,label){assert.ok(ok,label);checks++;console.log('PASS '+label);}
const reply={emotion:'happy',text:'Hello 🌞. A quote: "welcome" and नमस्ते!'};
const json=JSON.stringify(reply);
for(let size=1;size<=json.length;size++) {
  const decoder=new ReplyDecoder();let text='';
  for(let i=0;i<json.length;i+=size)text+=decoder.push(json.slice(i,i+size));
  assert.deepEqual(decoder.finish(),reply);assert.equal(text,reply.text);
}
check(true,'Every chunk width preserves JSON escapes and Unicode without duplication');
const escaped=new ReplyDecoder();let value='';for(const c of '{"text":"Hello \\uD83C\\uDF1E!","emotion":"happy"}')value+=escaped.push(c);
check(escaped.finish().text===value&&value==='Hello 🌞!','Split Unicode escape pairs decode to complete text');
for(const bad of ['{"text":"Hi","text":"Again","emotion":"happy"}','{"text":"Hi","emotion":"bad"}','{"text":"Hi"','{"text":"'+ 'x'.repeat(601)+'","emotion":"happy"}','{"text":"ok","emotion":"happy",}']) {
  assert.throws(()=>{const d=new ReplyDecoder();d.push(bad);d.finish();});
}
check(true,'Duplicate keys, invalid emotion, truncation, excess text and trailing comma rejected');
const events=[];let modelDone=false,earlySpeech=false;
const encode=x=>new TextEncoder().encode(JSON.stringify(x)+'\n');
async function* model() {
  yield encode({message:{content:'{"text":"First sentence. Second'},done:false});
  await new Promise(r=>setTimeout(r,20));
  check(events.some(e=>e.type==='audio'),'First voice clip emitted before model completion');
  yield encode({message:{content:' sentence.","emotion":"happy"}'},done:false});
  modelDone=true;yield encode({done:true,message:{content:''}});
}
await streamReply(model(),async text=>{earlySpeech||=!modelDone;return {pcm:'AA==',sampleRate:16000,cues:[],text};},e=>events.push(e),new AbortController().signal);
check(earlySpeech&&events[0].type==='delta','Actual deltas arrive and speech synthesis starts during inference');
check(events.filter(e=>e.type==='delta').map(e=>e.text).join('')===events.find(e=>e.type==='text').text,'Deltas reconstruct canonical final text');
check(events.filter(e=>e.type==='audio').map(e=>e.text).join(' ')==='First sentence. Second sentence.','Speech chunks cover complete text once');
check(events.at(-1).type==='done'&&events.at(-1).sequence===2,'Completion follows all audio with exact count');
const cancelled=new AbortController();cancelled.abort();await assert.rejects(()=>streamReply(model(),async()=>({}),()=>{},cancelled.signal));
check(true,'Cancelled stream cannot emit or synthesize');
async function* truncated(){yield encode({message:{content:'{"text":"Hi"'}});}
await assert.rejects(()=>streamReply(truncated(),async()=>({}),()=>{},new AbortController().signal));
check(true,'Missing model completion is rejected');
async function* many(){yield encode({message:{content:JSON.stringify({text:'One. Two. Three. Four.',emotion:'neutral'})},done:true});}
const manyEvents=[];await streamReply(many(),async()=>({}),e=>manyEvents.push(e),new AbortController().signal);
check(manyEvents.filter(e=>e.type==='audio').length===3&&manyEvents.filter(e=>e.type==='audio').map(e=>e.text).join(' ')==='One. Two. Three. Four.','Many sentences stay bounded and preserve all speech text');
let attempted=0;const failedEvents=[];
await assert.rejects(()=>streamReply(many(),async()=>{attempted++;throw new Error('speech_unavailable');},e=>failedEvents.push(e),new AbortController().signal),/speech_unavailable/);
check(attempted===1&&!failedEvents.some(e=>e.type==='done'),'Speech failure preserves original error and prevents subsequent synthesis/completion');
console.log(`PASS ${checks} progressive stream checks`);
