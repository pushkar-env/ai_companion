import copy, json, pathlib
from jsonschema import Draft202012Validator, FormatChecker
r=pathlib.Path(__file__).resolve().parents[1]
schemas={p.stem:json.loads(p.read_text()) for p in (r/'packages/contracts').glob('*.schema.json')}
for s in schemas.values(): Draft202012Validator.check_schema(s)
def validate(name,value): Draft202012Validator(schemas[name+'.schema'],format_checker=FormatChecker()).validate(value)
def reject(name,value):
    if not list(Draft202012Validator(schemas[name+'.schema'],format_checker=FormatChecker()).iter_errors(value)): raise AssertionError('Invalid fixture accepted: '+name)
send={'client_message_id':'11111111-1111-4111-8111-111111111111','text':'Can we plan a relaxing evening?'}
validate('SendMessage',send)
for text in ['','  ', 'a'*8001]: reject('SendMessage',dict(send,text=text))
reject('SendMessage',dict(send,role='system')); reject('SendMessage',dict(send,client_message_id='invalid'))
events=json.loads((r/'artifacts/events.json').read_text(encoding='utf-8'))
for e in events:
    validate('TextEvent',e)
    assert len(json.dumps(e,ensure_ascii=False).encode())<=32768
e=copy.deepcopy(events[1])
for key,value in [('seq',-1),('occurred_at','not-a-time'),('schema_version',2),('event_id','bad')]: reject('TextEvent',dict(e,**{key:value}))
bad=copy.deepcopy(e);bad['payload']['offset']=-1;reject('TextEvent',bad)
bad=copy.deepcopy(e);bad['payload'].pop('text');reject('TextEvent',bad)
# Full text budget and codepoint offsets across emitted producer sequences.
groups={}
for e in events: groups.setdefault(e['aggregate_id'],[]).append(e)
for stream in groups.values():
    text='';terminals=0
    for seq,e in enumerate(stream):
        assert e['seq']==seq
        if e['type']=='turn.text.delta': assert e['payload']['offset']==len(text); text+=e['payload']['text']
        if e['type']=='turn.completed': assert e['payload']['text']==text;terminals+=1
        if e['type']=='turn.failed': assert e['payload']['partial_text']==text;terminals+=1
    assert terminals==1 and len(text)<=16000
print(f'PASS schema definitions, formats, negative fixtures, Unicode continuity and wire budgets ({len(events)} generated events)')
resources=schemas['resources.schema']
def resource_validator(name):return Draft202012Validator({'$defs':resources['$defs'],'$ref':'#/$defs/'+name},format_checker=FormatChecker())
v=resource_validator('SupplementalClientEvent')
viseme=dict(events[0],type='avatar.visemes',payload={'utterance_id':send['client_message_id'],'playback_epoch':1,'frames':[{'offset_ms':0,'duration_ms':100,'weights':{'aa':.5,'sil':0}}]})
v.validate(viseme)
bad=copy.deepcopy(viseme);bad['payload']['frames'][0]['weights']['unknown']=.5;assert list(v.iter_errors(bad))
bad=copy.deepcopy(viseme);bad['payload']['frames'][0]['weights']['aa']=2;assert list(v.iter_errors(bad))
bad=copy.deepcopy(viseme);bad['payload']['frames']*=201;assert list(v.iter_errors(bad))
voice=dict(events[0],type='voice.state',payload={'session_id':send['client_message_id'],'playback_epoch':0,'state':'requesting_permission'});v.validate(voice)
voice['payload']['state']='made_up';assert list(v.iter_errors(voice))
internal=dict(events[0],type='memory.deleted',owner_id=send['client_message_id'],payload={'memory_id':send['client_message_id'],'memory_epoch':1});resource_validator('InternalEvent').validate(internal)
internal['payload']['text']='private transcript';assert list(resource_validator('InternalEvent').iter_errors(internal))
traits={k:.5 for k in ['warmth','humor','energy','curiosity','directness']};resource_validator('Traits').validate(traits)
traits['warmth']=2;assert list(resource_validator('Traits').iter_errors(traits))
print('PASS supplemental voice/viseme/internal-event schemas, enum/range/size and sensitive-payload negatives')
# Resolve every local OpenAPI reference and require distinct operation identities.
api=json.loads((r/'packages/contracts/openapi.json').read_text());ops=[]
def walk(x):
    if isinstance(x,dict):
        if '$ref' in x:
            target=api
            for part in x['$ref'].removeprefix('#/').split('/'):target=target[part]
        for value in x.values():walk(value)
    elif isinstance(x,list):
        for value in x:walk(value)
walk(api)
for path,methods in api['paths'].items():
    assert path.startswith('/v1/')
    for method,operation in methods.items():ops.append(operation['operationId'])
assert len(ops)==len(set(ops))
print('PASS OpenAPI local references and operation identities')
