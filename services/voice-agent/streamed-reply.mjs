// Incremental decoder for the constrained {text:string, emotion:string} response.
// Never expose JSON syntax or a half-decoded escape/surrogate to the conversation.
export class ReplyDecoder {
  state='start'; raw=''; key=''; text=''; values={}; escaped=false; bytes=0;
  push(chunk) {
    const before=this.text;
    for(const c of chunk) {
      if(++this.bytes>8192)throw new Error('invalid_model_reply');
      if(this.state==='keyString'||this.state==='valueString') {
        if(c==='"'&&!this.escaped) {
          let value;try{value=JSON.parse('"'+this.raw+'"');}catch{throw new Error('invalid_model_reply');}
          if(this.state==='keyString') {
            if(!['text','emotion'].includes(value)||Object.hasOwn(this.values,value))throw new Error('invalid_model_reply');
            this.key=value;this.state='colon';
          } else {this.values[this.key]=value;this.state='separator';}
          this.raw='';continue;
        }
        this.raw+=c;
        this.escaped=c==='\\'&&!this.escaped;
        if(this.state==='valueString'&&this.key==='text') {
          try {
            let value=JSON.parse('"'+this.raw+'"');
            if(/[\uD800-\uDBFF]$/.test(value))value=value.slice(0,-1);
            if(value.length>600)throw new RangeError();
            this.text=value;
          }catch(e){if(e instanceof RangeError)throw new Error('invalid_model_reply');}
        }
        continue;
      }
      if(/\s/.test(c))continue;
      if(this.state==='start'&&c==='{'){this.state='key';continue;}
      if(this.state==='key'&&c==='"'){this.state='keyString';continue;}
      if(this.state==='colon'&&c===':'){this.state='value';continue;}
      if(this.state==='value'&&c==='"'){this.state='valueString';continue;}
      if(this.state==='separator'&&c===','){this.state='key';continue;}
      if(this.state==='separator'&&c==='}'){this.state='end';continue;}
      throw new Error('invalid_model_reply');
    }
    return this.text.slice(before.length);
  }
  finish() {
    if(this.state!=='end'||typeof this.values.text!=='string'||!this.text.trim()||this.values.text!==this.text||!['neutral','happy','concerned','curious'].includes(this.values.emotion))throw new Error('invalid_model_reply');
    return this.values;
  }
}

export async function streamReply(body, synthesize, emit, signal) {
  const decoder=new ReplyDecoder(), utf8=new TextDecoder('utf-8',{fatal:true});
  let pending='',deltaSequence=0,audioSequence=0,consumed=0,scheduled=0,done=false;
  let work=Promise.resolve(),speechError;
  function enqueue(text) {
    scheduled++;
    work=work.then(async()=>{
      if(speechError)throw speechError;
      if(signal.aborted)throw new Error('cancelled');
      const speech=await synthesize(text);
      if(signal.aborted)throw new Error('cancelled');
      emit({type:'audio',sequence:audioSequence++,text,emotion:decoder.values.emotion||'neutral',...speech});
    }).catch(error=>{speechError??=error;});
  }
  function accept(line) {
    if(done)throw new Error('invalid_model_reply');
    const frame=JSON.parse(line);
    if(frame.error)throw new Error('local_model_unavailable');
    const delta=decoder.push(frame.message?.content||'');
    if(delta)emit({type:'delta',sequence:deltaSequence++,text:delta});
    // Hold the last segment until a following sentence exists; cap total TTS jobs at 3.
    const segments=[...new Intl.Segmenter('en',{granularity:'sentence'}).segment(decoder.text.slice(consumed))];
    for(let i=0;i<segments.length-1&&scheduled<2;i++) {
      const segment=segments[i].segment;consumed+=segment.length;
      if(segment.trim())enqueue(segment.trim());
    }
    if(frame.done)done=true;
  }
  try {
    for await(const chunk of body) {
      if(signal.aborted||speechError)throw speechError||new Error('cancelled');
      pending+=utf8.decode(chunk,{stream:true});
      if(pending.length>32768)throw new Error('invalid_model_reply');
      let end;while((end=pending.indexOf('\n'))>=0){const line=pending.slice(0,end);pending=pending.slice(end+1);if(line.trim())accept(line);}
    }
    pending+=utf8.decode();if(pending.trim())accept(pending);
    if(!done)throw new Error('invalid_model_reply');
    const reply=decoder.finish();emit({type:'text',...reply});
    const tail=decoder.text.slice(consumed).trim();if(tail)enqueue(tail);
    await work;if(speechError)throw speechError;
    if(!audioSequence)throw new Error('speech_unavailable');
    emit({type:'done',sequence:audioSequence});
  } catch(error) {throw error;}
}
