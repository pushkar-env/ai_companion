// Generated speech only. Compare adapters without recording the owner's microphone.
import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
function task(exe,args,input){return new Promise((resolve,reject)=>{const p=spawn(exe,args,{windowsHide:true,stdio:['pipe','pipe','pipe']});let output='';p.stdout.on('data',c=>output+=c);p.stderr.resume();p.on('error',reject);p.on('close',code=>code?reject(new Error('local speech task failed')):resolve(JSON.parse(output.replace(/^\uFEFF/,''))));p.stdin.end(JSON.stringify(input));});}
const ps=script=>['-NoProfile','-NonInteractive','-File','tools/'+script];
const phrases=['Hello, how are you today?', 'Could you help me plan my weekend?', 'I feel a little nervous about my presentation tomorrow.', 'Please remind me to drink water after lunch.', 'I would like to talk about my favourite music.'];
function words(s){return s.toLowerCase().match(/[a-z]+/g)??[];}
function errors(reference,hypothesis){const a=words(reference),b=words(hypothesis);let row=Array.from({length:b.length+1},(_,i)=>i);for(let i=1;i<=a.length;i++){const next=[i];for(let j=1;j<=b.length;j++)next[j]=Math.min(next[j-1]+1,row[j]+1,row[j-1]+(a[i-1]===b[j-1]?0:1));row=next;}return row[b.length];}
let winErrors=0,whisperErrors=0,total=0;const lines=[];
for(const text of phrases){const audio=await task('powershell.exe',ps('speak-local.ps1'),{text});const input={pcm:audio.pcm,sampleRate:audio.sampleRate};const win=await task('powershell.exe',ps('transcribe-local.ps1'),input);const whisper=await task('artifacts/whisper-env/Scripts/python.exe',['tools/transcribe-whisper.py'],input);winErrors+=errors(text,win.text);whisperErrors+=errors(text,whisper.text);total+=words(text).length;lines.push(`Generated: ${text}\nWindows: ${win.text}\nWhisper: ${whisper.text}`);}
lines.push(`Word edit errors: Windows ${winErrors}/${total}; Whisper ${whisperErrors}/${total}. Synthetic English corpus only; not a user-accent accuracy benchmark.`);
await mkdir('docs/evidence/m1/transcription-quality',{recursive:true});await writeFile('docs/evidence/m1/transcription-quality/comparison.txt',lines.join('\n\n')+'\n');console.log(lines.join('\n\n'));
