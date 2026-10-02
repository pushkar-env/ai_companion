import { CompanionClient } from '../../packages/contracts/generated/client.ts';
import type { TextEvent, TurnAccepted } from '../../packages/contracts/generated/contracts.ts';
function check(value:boolean,message:string):void {if(!value)throw new Error(message);}
const text='a🌿हिन्दी';check(Array.from(text).length===8,'Unicode code points differ from C#');
let call='';
const client=new CompanionClient({async request<T>(method:string,path:string,body:unknown,key:string|null) {
  call=method+' '+path+' '+key;
  return {turn_id:'22222222-2222-4222-8222-222222222222',user_message_id:'11111111-1111-4111-8111-111111111111',status:'accepted',events_url:'/v1/turns/22222222-2222-4222-8222-222222222222/events'} as T;
}});
async function run() {
  const accepted:TurnAccepted=await client.PostTurnAccepted('11111111-1111-4111-8111-111111111111',{client_message_id:'11111111-1111-4111-8111-111111111111',text},'33333333-3333-4333-8333-333333333333');
  check(accepted.status==='accepted'&&call.startsWith('POST /v1/conversations/'),'Generated client route');
  const e:TextEvent={event_id:'e',schema_version:1,type:'turn.text.delta',aggregate_id:'a',seq:0,occurred_at:'2026-09-25T10:00:00Z',trace_id:'mock',payload:{offset:0,text}};
  check(Array.from(e.payload.text??'').length===8,'Typed payload Unicode');
  console.log('PASS generated TypeScript client transport and Unicode contract consumer');
}
void run();
