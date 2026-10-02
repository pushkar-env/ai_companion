"""One-time initial schema construction from normative seeds and M0 field inventory."""
import json, pathlib, re
r=pathlib.Path(__file__).resolve().parents[1]
out=r/'packages/contracts'; out.mkdir(parents=True,exist_ok=True)
blocks=re.findall(r'```json\s*([\s\S]*?)```',(r/'docs/requirements/29_FORMAL_SCHEMA_SEEDS.md').read_text(encoding='utf-8'))
chat,event=map(json.loads,blocks[:2])
defs=chat['$defs']; defs['TextEvent']=event.copy()
defs['TextEvent'].pop('$schema'); defs['TextEvent'].pop('$id')
payload={}
for variant in event['oneOf']: payload.update(variant['properties']['payload']['properties'])
defs['EventPayload']={'type':'object','properties':payload}
defs['TextEvent']['properties']['payload']={'$ref':'#/$defs/EventPayload'}
defs['TurnState']={'type':'object','required':['turn_id','conversation_id','status','last_seq','text'], 'properties':{
 'turn_id':{'type':'string','format':'uuid'},'conversation_id':{'type':'string','format':'uuid'},
 'status':{'type':'string','enum':['accepted','streaming','completed','failed','cancelled']},
 'assistant_message_id':{'type':'string'},'last_seq':{'type':'integer','minimum':0},
 'failure_code':{'type':'string'},'text':{'type':'string','maxLength':16000}}}
chat.pop('$ref'); chat['$id']='urn:companion:chat-contract:1'
(out/'chat.schema.json').write_text(json.dumps(chat,indent=2)+'\n')
for name in ['SendMessage','TurnAccepted','TextEvent','TurnState']:
    # Self-contained entrypoints keep validator and codegen resolution offline.
    entry={'$schema':chat['$schema'],'$defs':defs,'$ref':'#/$defs/'+name}
    (out/(name+'.schema.json')).write_text(json.dumps(entry,indent=2)+'\n')
api={'openapi':'3.1.0','info':{'title':'AI Companion local mock API','version':'0.1.0','description':'M0 subset. Loopback only, fake identity, volatile data. Not a production service.'},'servers':[{'url':'http://localhost:8080'}], 'paths':{},'components':{'schemas':defs}}
for method,path,name in [('post','/v1/conversations/{id}/messages','TurnAccepted'),('get','/v1/turns/{id}','TurnState'),('get','/v1/turns/{id}/events','TextEvent'),('post','/v1/turns/{id}/cancel','TurnState')]:
    op={'operationId':method+name, 'parameters':[{'name':'id','in':'path','required':True,'schema':{'type':'string','format':'uuid'}}], 'responses':{'202' if name=='TurnAccepted' else '200':{'description':name,'content':{'text/event-stream' if name=='TextEvent' else 'application/json':{'schema':{'$ref':'#/components/schemas/'+name}}}}, '400':{'description':'Invalid input'},'404':{'description':'Resource not found'},'409':{'description':'Idempotency or active-turn conflict'}}}
    if name=='TurnAccepted':
        op['parameters'].append({'name':'Idempotency-Key','in':'header','required':True,'schema':{'type':'string','format':'uuid'}})
        op['requestBody']={'required':True,'content':{'application/json':{'schema':{'$ref':'#/components/schemas/SendMessage'}}}}
    api['paths'][path]={method:op}
def refs(x):
    if isinstance(x,dict):
        for k,v in x.items():
            if k=='$ref': x[k]=v.replace('#/$defs/','#/components/schemas/')
            else: refs(v)
    elif isinstance(x,list):
        for v in x: refs(v)
refs(api)
(out/'openapi.json').write_text(json.dumps(api,indent=2)+'\n')
