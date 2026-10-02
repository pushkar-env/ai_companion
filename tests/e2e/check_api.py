"""Loopback smoke tests; starts and terminates only its own API child process."""
import json, os, pathlib, socket, subprocess, time, urllib.request, urllib.error, uuid
r=pathlib.Path(__file__).resolve().parents[2]
with socket.socket() as probe:
    if probe.connect_ex(('127.0.0.1',8080))==0: raise SystemExit('BLOCKED: port 8080 is in use; stop your local mock API and rerun.')
env=dict(os.environ,APP_ENV='local',MOCK_EXTERNAL_SERVICES='true')
p=subprocess.Popen(['dotnet',str(r/'services/api/bin/Debug/net10.0/Companion.MockApi.dll')],cwd=r,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
base='http://127.0.0.1:8080'
def request(path,body=None,headers=None):
    req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json',**(headers or {})})
    try:
        with urllib.request.urlopen(req,timeout=10) as response: return response.status,response.read().decode()
    except urllib.error.HTTPError as error: return error.code,error.read().decode()
def data(path,body=None,headers=None):
    status,text=request(path,body,headers);return status,json.loads(text) if text else None
try:
    for i in range(80):
        try:
            if data('/health')[0]==200: break
        except OSError: pass
        if p.poll() is not None:
            error=p.stderr.read().decode(errors='replace')
            if 'Application Control policy' in error:
                print('BLOCKED: Windows Application Control rejected the local API DLL. Obtain normal host/IT trust approval; no HTTP checks executed for this binary.')
                raise SystemExit(77)
            raise AssertionError('API exited before readiness (exit '+str(p.returncode)+'). Run the local API directly for diagnostics.')
        time.sleep(.1)
    else: raise AssertionError('API readiness timeout')
    conv=str(uuid.uuid4());path='/v1/conversations/'+conv+'/messages'
    body={'client_message_id':str(uuid.uuid4()),'text':'hello 🌿'};headers={'Idempotency-Key':str(uuid.uuid4())}
    status,accepted=data(path,body,headers);assert status==202
    assert data(path,body,headers)==(202,accepted)
    assert data(path,dict(body,text='changed'),headers)[0]==409
    assert data(path,dict(body,role='system'),headers)[0]==400
    assert data(path,dict(body,text='  '),headers)[0]==400
    assert data(path,body,{'Idempotency-Key':str(uuid.uuid4())})==(202,accepted)
    assert data(path,dict(body,client_message_id=str(uuid.uuid4())),{'Idempotency-Key':str(uuid.uuid4())})[0]==409
    status,sse=request(accepted['events_url']);assert status==200
    events=[json.loads(line[6:]) for line in sse.splitlines() if line.startswith('data: ')]
    assert events[-1]['type']=='turn.completed'
    assert len({e['event_id'] for e in events})==len(events)
    canonical=data('/v1/turns/'+accepted['turn_id'])[1]
    assert canonical['text']==events[-1]['payload']['text'] and canonical['status']=='completed'
    replay=request(accepted['events_url'],headers={'Last-Event-ID':events[1]['event_id']})[1]
    assert [json.loads(l[6:]) for l in replay.splitlines() if l.startswith('data: ')]==events[2:]
    assert request(accepted['events_url'],headers={'Last-Event-ID':str(uuid.uuid4())})[0]==409
    second=data(path,dict(body,client_message_id=str(uuid.uuid4())),{'Idempotency-Key':str(uuid.uuid4())})[1]
    cancel='/v1/turns/'+second['turn_id']+'/cancel'
    assert data(cancel,{})[1]['status']=='cancelled';assert data(cancel,{})[1]['status']=='cancelled'
    remaining=request(second['events_url'])[1];assert 'turn.cancelled' in remaining and 'turn.text.delta' not in remaining
    (r/'artifacts/api-events.json').write_text(json.dumps(events,indent=2),encoding='utf-8')
    print('PASS API HTTP admission, unknown fields, whitespace, duplicate action/message, conflict, SSE, replay, canonical text, cancellation')
finally:
    p.terminate()
    try: p.wait(timeout=5)
    except subprocess.TimeoutExpired: p.kill();p.wait()
# Separate negative startup gate, no credentials or integration mode.
bad=subprocess.run(['dotnet',str(r/'services/api/bin/Debug/net10.0/Companion.MockApi.dll')],env=dict(env,APP_ENV='production'),stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=10)
assert bad.returncode!=0
print('PASS production startup rejects local mock executable')
