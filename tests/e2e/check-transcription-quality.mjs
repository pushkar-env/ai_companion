import { readFile, mkdir, writeFile } from 'node:fs/promises';
const session=JSON.parse(await readFile(new URL('../../artifacts/talking-character/session.json',import.meta.url),'utf8'));
const fixture=JSON.parse(await readFile(new URL('../../artifacts/voice-input/synthetic-speech.json',import.meta.url),'utf8'));
const clean=Buffer.from(fixture.pcm,'base64');
const quiet=Buffer.from(clean);for(let i=0;i<quiet.length;i+=2)quiet.writeInt16LE(Math.round(quiet.readInt16LE(i)*.02),i);
const cases=[['clean',clean],['quiet',quiet],['five-second lead',Buffer.concat([Buffer.alloc(160000),clean])],['four-second pause',Buffer.concat([clean,Buffer.alloc(128000),clean])],['silence',Buffer.alloc(32000)]];
const baseline=process.argv.includes('--baseline');const lines=[];let failed=false;
for(const [name,pcm] of cases){
  const response=await fetch(session.url+'/transcribe',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+session.token},body:JSON.stringify({pcm:pcm.toString('base64'),sampleRate:16000}),signal:AbortSignal.timeout(35000)});
  const result=await response.json();
  const words=(result.text??'').toLowerCase().match(/[a-z]+/g)??[];
  const expected='please tell me a short story about the sunshine'.split(' ');if(name==='four-second pause')expected.push(...expected.slice());
  const exact=name==='silence'?words.length===0:words.join(' ')===expected.join(' ');
  const expectedWarning=name==='quiet'?'quiet':name==='silence'?'no_signal':'';
  const ok=response.ok&&exact&&(baseline||(result.mode==='local-whisper-english'&&(!expectedWarning||result.warning===expectedWarning)));failed||=!ok;
  lines.push(`${ok?'PASS':'FAIL'} ${name}: exact synthetic phrase=${exact}; confidence=${result.confidence}; warning=${result.warning??'none'}; engine=${result.mode}`);
}
const folder=new URL('../../docs/evidence/m1/transcription-quality/',import.meta.url);await mkdir(folder,{recursive:true});
await writeFile(new URL(baseline?'before.txt':'after.txt',folder),lines.join('\n')+'\n');console.log(lines.join('\n'));
if(failed&&!baseline)process.exitCode=1;
