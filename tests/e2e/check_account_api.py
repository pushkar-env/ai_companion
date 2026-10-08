"""Real local HTTP + PostgreSQL checks; invoked by check-database.py --api.
All IDs/content are synthetic. Never writes credentials outside ignored scratch files.
"""
import datetime
import json
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.request
import uuid


def run(root, work, db_port, password, query, hidden):
    query("CREATE ROLE companion_api LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD '"+password+"'; GRANT companion_runtime TO companion_api;")
    budget='50000000-0000-0000-0000-000000000009'
    query(f"""INSERT INTO companion.global_budgets(id,unit_type,limit_units,starts_at,ends_at)
    VALUES('{budget}','synthetic_http_units',30,now()-interval '1 day',now()+interval '1 day');
    INSERT INTO companion.user_budgets(user_id,budget_id,limit_units) SELECT id,'{budget}',20 FROM companion.users;""")
    owners=['00000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002']
    conversations=[str(uuid.uuid4()) for _ in range(5)]
    for i,conv in enumerate(conversations):
        owner=owners[0 if i<3 else 1]
        companion='10000000-0000-0000-0000-00000000000'+('1' if i<3 else '2')
        query(f"INSERT INTO companion.conversations(id,user_id,companion_id) VALUES('{conv}','{owner}','{companion}');")
    with socket.socket() as sock:
        sock.bind(('127.0.0.1',0)); port=sock.getsockname()[1]
    tokens=[secrets.token_hex(32),secrets.token_hex(32)]
    config={'Port':port,'Database':f'Host=127.0.0.1;Port={db_port};Username=companion_api;Password={password};Database=postgres',
            'Budget':budget,'Units':10,'ExpiresAt':(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(minutes=15)).isoformat(),
            'Accounts':[{'UserId':owner,'Token':token} for owner,token in zip(owners,tokens)]}
    fixture=work/'account-fixture.json'; fixture.write_text(json.dumps(config),encoding='utf-8')
    import os
    env={**os.environ,'APP_ENV':'local','SYNTHETIC_ACCOUNTS_ONLY':'true','COMPANION_ACCOUNT_FIXTURE':str(fixture)}
    process=None; results=[]
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
    def request(path,body=None,account=0,extra=None):
        headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+tokens[account]} if account is not None else {}),**(extra or {})}
        req=urllib.request.Request(f'http://127.0.0.1:{port}'+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
        try:
            response=opener.open(req,timeout=10)
        except urllib.error.HTTPError as error:
            response=error
        with response:
            raw=response.read()
            return response.status,json.loads(raw) if raw else None,response.headers
    def stream(path,account=0,extra=None):
        req=urllib.request.Request(f'http://127.0.0.1:{port}'+path,
            headers={'Authorization':'Bearer '+tokens[account],**(extra or {})})
        return opener.open(req,timeout=8)
    def frame(response):
        fields={}
        while True:
            line=response.readline()
            if not line:return None
            line=line.decode('utf-8').rstrip('\r\n')
            if not line:return fields
            if not line.startswith(':'):
                name,_,value=line.partition(':');fields[name]=value.lstrip()
    def next_event(response):
        for _ in range(20):
            value=frame(response)
            if value is None or 'data' in value:return value
        raise AssertionError('SSE event timeout')
    def start():
        nonlocal process
        process=subprocess.Popen(['dotnet',str(root/'services/account-api/bin/Debug/net10.0/Companion.AccountApi.dll')],cwd=root,env=env,
                                 stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=hidden)
        for _ in range(80):
            if process.poll() is not None: raise RuntimeError('Account API exited before readiness; configuration or host execution requires inspection')
            try:
                if request('/health')[0]==200:return
            except OSError: pass
            time.sleep(.1)
        raise RuntimeError('Account API readiness timeout')
    def stop():
        if process and process.poll() is None:
            process.terminate(); process.wait(timeout=10)
    def check(ok,label):
        if not ok: raise AssertionError(label)
        results.append('PASS '+label)
    path='/local/v1/conversations/'+conversations[0]+'/admissions'
    body={'client_message_id':str(uuid.uuid4()),'text':'Synthetic hello नमस्ते 🌼'}
    key={'Idempotency-Key':str(uuid.uuid4())}
    try:
        start()
        check(request(path,body,account=None,extra=key)[0]==401,'HTTP missing identity denied')
        check(request(path,body,extra={**key,'Authorization':'Bearer invalid'})[0]==401,'HTTP invalid token denied')
        check(request(path,body,extra={**key,'Origin':'https://example.invalid'})[0]==403,'HTTP browser-origin request denied')
        check(request(path,body,extra={**key,'Host':'example.invalid'})[0]==403,'HTTP foreign Host denied')
        check(request(path,{**body,'user_id':owners[1]},extra=key)[0]==400,'HTTP body cannot supply actor')
        check(request(path,body,account=1,extra=key)[0]==404,'HTTP other account cannot admit to owner conversation')
        status,accepted,headers=request(path,body,extra=key)
        check(status==202 and headers['Cache-Control']=='no-store','HTTP authenticated admission accepted without cache')
        check(request(path,body,extra=key)[:2]==(202,accepted),'HTTP same-key retry reuses accepted turn')
        check(request(path,body,extra={'Idempotency-Key':str(uuid.uuid4())})[:2]==(202,accepted),'HTTP same-client retry reuses turn and quota hold')
        check(request(path,{**body,'text':'Different synthetic text'},extra=key)[0]==409,'HTTP changed payload conflicts')
        check(request(path,{**body,'client_message_id':str(uuid.uuid4())},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==409,'HTTP active-turn rejection strands no second hold')
        turn_path='/local/v1/turns/'+accepted['turn_id']
        isolated=True
        for _ in range(5):
            isolated &= request(turn_path,account=0)[0]==200 and request(turn_path,account=1)[0]==404
        check(isolated,'HTTP pooled connection preserves owner isolation across five alternations')
        check(request(turn_path,account=1,extra={'X-User-Id':owners[0]})[0]==404,'HTTP owner header cannot override authenticated actor')
        check(request(path,{**body,'text':'x'*8001},extra=key)[0]==400 and request(path,{**body,'text':'  '},extra=key)[0]==400,'HTTP oversized and blank text rejected')
        own=request(turn_path)[1]
        check(own['reservation_id'] is not None and query(f"SELECT count(*) FROM companion.usage_reservations WHERE budget_id='{budget}';").strip()=='1','HTTP turn is bound to exactly one durable reservation')
        second='/local/v1/conversations/'+conversations[1]+'/admissions'
        check(request(second,{'client_message_id':str(uuid.uuid4()),'text':'Synthetic second'},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==202,'HTTP second conversation reserves remaining account units')
        before=query('SELECT count(*) FROM companion.turns;')
        check(request('/local/v1/conversations/'+conversations[2]+'/admissions',{'client_message_id':str(uuid.uuid4()),'text':'Over account cap'},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==429,'HTTP account quota exhausted returns 429')
        check(query('SELECT count(*) FROM companion.turns;')==before,'HTTP quota denial rolls back turn acceptance')
        check(request('/local/v1/conversations/'+conversations[3]+'/admissions',{'client_message_id':str(uuid.uuid4()),'text':'Synthetic other owner'},account=1,extra={'Idempotency-Key':str(uuid.uuid4())})[0]==202,'HTTP second account uses shared remaining cap')
        check(request('/local/v1/conversations/'+conversations[4]+'/admissions',{'client_message_id':str(uuid.uuid4()),'text':'Over global cap'},account=1,extra={'Idempotency-Key':str(uuid.uuid4())})[0]==429,'HTTP global quota exhausted returns 429')
        stop();start()
        check(request(path,body,extra=key)[:2]==(202,accepted),'HTTP restart preserves idempotent result at exhausted quota')
        events='/local/v1/conversations/'+conversations[0]+'/events'
        messages='/local/v1/conversations/'+conversations[0]+'/messages'
        cancel=turn_path+'/cancel'
        check(request(events,account=1)[0]==404 and request(messages,account=1)[0]==404,
              'HTTP event and message history deny other owner')
        check(all(request(events+suffix)[0]==400 for suffix in ['?after=-1','?after=99','?limit=0','?limit=101']),
              'HTTP replay rejects invalid cursors and page bounds')
        first=request(events)[1]
        check(len(first['items'])==1 and first['items'][0]['type']=='turn.accepted' and first['items'][0]['text']==body['text'],
              'HTTP persisted accepted event replays canonical Unicode text')
        check(request(cancel,{'expected_version':1},account=1)[0]==404 and request(cancel,{'expected_version':2})[0]==409,
              'HTTP cancellation rejects wrong owner and stale version')
        check(request(cancel,{'expected_version':1,'actual_units':0})[0]==400,
              'HTTP cancellation cannot choose usage settlement')
        worker="BEGIN; SET LOCAL ROLE companion_worker; SELECT set_config('companion.user_id','"+owners[0]+"',true);"
        # Claim the oldest funded turn for this owner; prior worker tests leave no accepted work.
        claim=query(worker+"SELECT turn_id,lease_token FROM companion.claim_local_turn(gen_random_uuid(),60); COMMIT;").strip().splitlines()[-1].split('|')
        check(claim[0]==accepted['turn_id'],'HTTP accepted work is claimable by trusted worker')
        check(request(cancel,{'expected_version':1})[0]==200 and request(cancel,{'expected_version':1})[0]==200,
              'HTTP cancellation and exact retry succeed once')
        try:
            query(worker+f"SELECT companion.finish_local_turn('{claim[0]}','{claim[1]}',1,'completed','Late synthetic reply',2); COMMIT;")
            late_denied=False
        except RuntimeError as error:
            late_denied='terminal_conflict' in str(error)
        check(late_denied and query(f"SELECT count(*) FROM companion.messages WHERE turn_id='{claim[0]}' AND role='assistant';").strip()=='0',
              'HTTP cancellation fences a previously claimed worker late reply')
        check(query(f"SELECT state,settled_units IS NULL FROM companion.usage_reservations WHERE id='{own['reservation_id']}';").strip()=='reserved|t',
              'HTTP cancellation preserves hold until trusted reconciliation')
        page=request(events+'?limit=1')[1]
        tail=request(events+'?after='+str(page['next_cursor'])+'&limit=1')[1]
        check(page['has_more'] and tail['items'][0]['type']=='turn.cancelled' and tail['next_cursor']==2 and not tail['has_more'],
              'HTTP bounded replay resumes without duplicate events')
        check(request(events+'?after=2')[1]['items']==[] and request(messages)[1]['items'][0]['status']=='cancelled',
              'HTTP terminal cursor returns empty page and canonical history reflects cancellation')
        query(worker+f"SELECT companion.finish_local_turn('{claim[0]}','{claim[1]}',1,'cancelled',NULL,2); COMMIT;")
        check(query(f"SELECT count(*),sum(units) FROM companion.usage_ledger WHERE reservation_id='{own['reservation_id']}';").strip()=='1|2'
              and len(request(events)[1]['items'])==2,'HTTP trusted cancellation reconciliation settles once without another event')
        # Complete the second admitted conversation and verify the opposite race outcome.
        claim2=query(worker+"SELECT turn_id,lease_token FROM companion.claim_local_turn(gen_random_uuid(),60); COMMIT;").strip().splitlines()[-1].split('|')
        query(worker+f"SELECT companion.finish_local_turn('{claim2[0]}','{claim2[1]}',1,'completed','[SYNTHETIC] Canonical reply',0); COMMIT;")
        history2='/local/v1/conversations/'+conversations[1]+'/messages'
        check(request('/local/v1/turns/'+claim2[0]+'/cancel',{'expected_version':1})[0]==409
              and [m['role'] for m in request(history2)[1]['items']]==['user','assistant'],
              'HTTP completion wins before cancellation and exposes one canonical assistant reply')
        # A rolled back admission must not consume a replay sequence.
        cursor_before=query(f"SELECT next_event_sequence FROM companion.conversations WHERE id='{conversations[0]}';")
        query(worker+f"SELECT companion.admit_metered_text('{conversations[0]}',gen_random_uuid(),gen_random_uuid(),'Rollback replay','{budget}',10); ROLLBACK;")
        check(query(f"SELECT next_event_sequence FROM companion.conversations WHERE id='{conversations[0]}';")==cursor_before
              and len(request(events)[1]['items'])==2,'HTTP rolled back admission leaves no event or cursor gap')
        snapshot=request(events)[1]
        query(worker+"SELECT companion.process_next_status_event() FROM generate_series(1,100); COMMIT;")
        stop();start()
        check(request(events)[1]==snapshot and len(request(history2)[1]['items'])==2,
              'HTTP restart and status consumption preserve event replay and canonical history')
        stream_path='/local/v1/conversations/'+conversations[0]+'/stream'
        check(request(stream_path,account=1)[0]==404 and request(stream_path,account=None)[0]==401,
              'HTTP SSE requires authenticated conversation owner')
        check(all(request(stream_path,extra={'Last-Event-ID':value})[0]==400 for value in ['-1','garbage','99'])
              and request(stream_path+'?after=0',extra={'Last-Event-ID':'1'})[0]==400,
              'HTTP SSE rejects malformed future and ambiguous resume cursors')
        with stream(stream_path) as response:
            a=next_event(response);b=next_event(response)
            check(response.headers['Content-Type']=='text/event-stream' and response.headers['Cache-Control']=='no-store'
                  and [a['id'],b['id']]==['1','2'] and json.loads(a['data'])['text']==body['text'],
                  'HTTP SSE replays ordered durable Unicode events with no caching')
        time.sleep(.6)
        with stream(stream_path,extra={'Last-Event-ID':'1'}) as response:
            resumed=next_event(response)
            check(resumed==b,'HTTP SSE reconnect resumes strictly after last received event')
        time.sleep(.6)
        live_path='/local/v1/conversations/'+conversations[3]+'/stream'
        live_turn=request('/local/v1/conversations/'+conversations[3]+'/events',account=1)[1]['items'][0]['turn_id']
        with stream(live_path+'?after=1',account=1) as response:
            frame(response) # Headers are flushed before new terminal work is committed.
            check(request('/local/v1/turns/'+live_turn+'/cancel',{'expected_version':1},account=1)[0]==200,
                  'HTTP SSE idle stream does not block concurrent cancellation')
            live=next_event(response)
            check(live['id']=='2' and live['event']=='turn.cancelled' and json.loads(live['data'])['turn_id']==live_turn,
                  'HTTP SSE delivers a newly committed event without reconnect')
        time.sleep(.6)
        opened=[]
        try:
            for _ in range(4):opened.append(stream(stream_path+'?after=2'))
            check(request(stream_path+'?after=2')[0]==429 and request(turn_path)[0]==200,
                  'HTTP SSE concurrency cap preserves ordinary API and database availability')
        finally:
            for response in opened:response.close()
        time.sleep(1)
        with stream(stream_path+'?after=2') as response:
            check(frame(response) is not None,'HTTP SSE disconnect releases stream admission slot')
        time.sleep(.6)
        client_project=root/'tests/account-client/Companion.AccountClient.Checks.csproj'
        built=subprocess.run(['dotnet','build',str(client_project)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                             creationflags=hidden,timeout=60)
        if built.returncode!=0:raise AssertionError('Synthetic client check build failed')
        client_result=subprocess.run(['dotnet',str(root/'tests/account-client/bin/Debug/net10.0/Companion.AccountClient.Checks.dll'),
                                      str(fixture),conversations[0]],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                                     text=True,creationflags=hidden,timeout=30)
        if client_result.returncode!=0:raise AssertionError('Synthetic client runtime checks failed; inspect locally without printing credentials')
        client_lines=[line for line in client_result.stdout.splitlines() if line.startswith('PASS client ')]
        if len(client_lines)!=13:raise AssertionError('Synthetic client evidence incomplete')
        results.extend(client_lines)
        time.sleep(.6)
        import sys
        if '--unity' in sys.argv or '--unity-runtime' in sys.argv or '--unity-commands' in sys.argv:
            unity_folder=root/'artifacts/unity-account';unity_folder.mkdir(parents=True,exist_ok=True)
            unity_fixture=unity_folder/'fixture.json';unity_result=unity_folder/('command-result.json' if '--unity-commands' in sys.argv else 'runtime-result.json' if '--unity-runtime' in sys.argv else 'result.json')
            if unity_fixture.exists():raise AssertionError('An active Unity fixture exists; stop its owning harness first')
            run_id=str(uuid.uuid4());unity_result.unlink(missing_ok=True)
            runtime_conversation=str(uuid.uuid4())
            query(f"INSERT INTO companion.conversations(id,user_id,companion_id) VALUES('{runtime_conversation}','{owners[0]}','10000000-0000-0000-0000-000000000001');")
            for index in range(6):
                prompt=f'[SYNTHETIC {index+1}] Tell me about a calm afternoon. '
                reply='[SYNTHETIC] '+('A walk, a warm drink, and time to read can make a quiet afternoon. '*16)
                query(worker+f"SELECT companion.finish_text(companion.accept_text('{runtime_conversation}',gen_random_uuid(),gen_random_uuid(),'{prompt}'),1,'completed','{reply}'); COMMIT;")
            if '--unity-commands' in sys.argv:
                query(f"UPDATE companion.global_budgets SET limit_units=limit_units+100 WHERE id='{budget}'; UPDATE companion.user_budgets SET limit_units=limit_units+100 WHERE budget_id='{budget}';")
            unity_fixture.write_text(json.dumps({'runId':run_id,'endpoint':f'http://127.0.0.1:{port}/',
                'token':tokens[0],'otherToken':tokens[1],'conversation':conversations[0],'runtimeConversation':runtime_conversation}),encoding='utf-8')
            try:
                print('READY: Run Companion/Run Durable Conversation Command Checks in stopped Editor.' if '--unity-commands' in sys.argv else 'READY: Unity fixture; run Companion/Run Populated History Layout Checks in Play mode.' if '--unity-runtime' in sys.argv else 'READY: Unity real-account fixture; run Companion/Run Real Local Account API Checks within 5 minutes.',flush=True)
                end=time.monotonic()+300
                while not unity_result.exists() and time.monotonic()<end:time.sleep(.25)
                if not unity_result.exists():raise AssertionError('Unity Editor verification timed out')
                result=json.loads(unity_result.read_text(encoding='utf-8'))
                check(result.get('runId')==run_id and result.get('passed') is True and result.get('checks')==(16 if '--unity-commands' in sys.argv else 16 if '--unity-runtime' in sys.argv else 8),
                      'HTTP actual API verified by durable Unity commands' if '--unity-commands' in sys.argv else 'HTTP actual API verified by populated runtime history' if '--unity-runtime' in sys.argv else 'HTTP actual account API verified by Unity history screen and owner switch')
            finally:unity_fixture.unlink(missing_ok=True)
        stop()
        config['ExpiresAt']=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=3)).isoformat()
        fixture.write_text(json.dumps(config),encoding='utf-8')
        start()
        with stream(stream_path+'?after=2') as response:
            began=time.monotonic()
            while frame(response) is not None:pass
            check(time.monotonic()-began<5,'HTTP SSE active connection closes at token expiry')
        time.sleep(.1)
        check(request(turn_path)[0]==401,'HTTP expired local token rejected after startup')
        stop()
        rejected=subprocess.run(['dotnet',str(root/'services/account-api/bin/Debug/net10.0/Companion.AccountApi.dll')],cwd=root,
            env={**env,'APP_ENV':'production'},stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=hidden,timeout=10)
        check(rejected.returncode!=0,'HTTP executable rejects production mode')
    finally:
        stop();fixture.unlink(missing_ok=True)
    return results
