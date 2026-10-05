"""Live PostgreSQL lease and bounded worker tests. All data is synthetic."""
import concurrent.futures
import os
import subprocess
import sys
import uuid


def run(root, query, env, password, hidden):
    lines=[]
    owner='00000000-0000-0000-0000-000000000001'
    budget=str(uuid.uuid4())
    query(f"INSERT INTO companion.global_budgets(id,unit_type,limit_units,starts_at,ends_at) VALUES('{budget}','synthetic_worker_units',100,now()-interval '1 day',now()+interval '1 day'); INSERT INTO companion.user_budgets(user_id,budget_id,limit_units) VALUES('{owner}','{budget}',100);")

    def worker(sql, actor=owner):
        return query(f"BEGIN; SET LOCAL ROLE companion_worker; SELECT set_config('companion.user_id','{actor}',true);"+sql+' COMMIT;').strip().splitlines()[1:]

    def admit():
        conversation=str(uuid.uuid4())
        query(f"INSERT INTO companion.conversations(id,user_id,companion_id) VALUES('{conversation}','{owner}','10000000-0000-0000-0000-000000000001');")
        return worker(f"SELECT companion.admit_metered_text('{conversation}',gen_random_uuid(),gen_random_uuid(),'Synthetic worker turn','{budget}',10);")[0]

    def claim():
        return worker(f"SELECT * FROM companion.claim_local_turn('{uuid.uuid4()}',60);")

    def finish(turn,token,version=1):
        return worker(f"SELECT companion.finish_local_turn('{turn}','{token}',{version},'completed','Synthetic fenced reply',2);")

    def rejected(operation, message):
        try:
            operation()
        except RuntimeError as error:
            assert message in str(error)
        else:
            raise AssertionError('operation unexpectedly accepted')

    assert query("SELECT has_function_privilege('companion_runtime','companion.claim_local_turn(uuid,integer)','EXECUTE') OR has_table_privilege('companion_runtime','companion.worker_leases','SELECT');").strip()=='f'
    lines.append('PASS account runtime cannot claim leases or read worker tokens')
    turn=admit()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        claims=list(pool.map(lambda _:claim(),range(2)))
    assert sum(bool(c) for c in claims)==1
    record=next(c[0] for c in claims if c); claimed,token,version=record.split('|')
    assert claimed==turn and version=='1'
    lines.append('PASS competing workers claim one accepted turn once')
    assert worker(f"SELECT companion.renew_local_turn('{turn}','{token}',60);")==['t']
    assert worker(f"SELECT companion.renew_local_turn('{turn}','{uuid.uuid4()}',60);")==['f']
    rejected(lambda:worker(f"SELECT companion.finish_local_turn('{turn}','{token}',1,'completed','Synthetic fenced reply',2);",'00000000-0000-0000-0000-000000000002'),'turn_unavailable')
    rejected(lambda:worker("SELECT * FROM companion.claim_local_turn(gen_random_uuid(),0);"),'invalid_lease')
    lines.append('PASS renewal requires current token and completion remains owner scoped')
    query(f"UPDATE companion.worker_leases SET expires_at=now()-interval '1 second' WHERE turn_id='{turn}';")
    rejected(lambda:finish(turn,token),'lease_lost')
    assert worker(f"SELECT companion.renew_local_turn('{turn}','{token}',60);")==['f']
    new_turn,new_token,_=claim()[0].split('|')
    assert new_turn==turn and new_token!=token
    rejected(lambda:finish(turn,token),'lease_lost')
    rejected(lambda:worker(f"SELECT companion.finish_local_turn('{turn}','{new_token}',1,'completed','Synthetic fenced reply',11);"),'usage_exceeds_reservation')
    assert query(f"SELECT state FROM companion.turns WHERE id='{turn}';").strip()=='accepted'
    assert query(f"SELECT finished FROM companion.worker_leases WHERE turn_id='{turn}';").strip()=='f'
    lines.append('PASS expired lease reclaims with new token and fences stale completion')
    lines.append('PASS failed settlement leaves lease unfinished and turn retryable')
    finish(turn,new_token)
    query(f"UPDATE companion.worker_leases SET expires_at=now()-interval '1 second' WHERE turn_id='{turn}';")
    finish(turn,new_token)
    assert query(f"SELECT count(*) FROM companion.usage_ledger WHERE reservation_id=(SELECT reservation_id FROM companion.turns WHERE id='{turn}');").strip()=='1'
    assert claim()==[]
    lines.append('PASS completed token receipt retries after expiry without duplicate charge')
    # Interrupting a claim transaction strands neither lease nor work.
    rolled=admit()
    query(f"BEGIN; SET LOCAL ROLE companion_worker; SELECT set_config('companion.user_id','{owner}',true); SELECT * FROM companion.claim_local_turn(gen_random_uuid(),60); ROLLBACK;")
    assert query(f"SELECT count(*) FROM companion.worker_leases WHERE turn_id='{rolled}';").strip()=='0'
    lines.append('PASS rolled-back claim leaves work available')
    query("CREATE ROLE companion_local_worker LOGIN NOSUPERUSER NOBYPASSRLS PASSWORD '"+password+"'; GRANT companion_worker TO companion_local_worker;")
    worker_env={**env,'PGUSER':'companion_local_worker','APP_ENV':'local','SYNTHETIC_ACCOUNTS_ONLY':'true','COMPANION_SYNTHETIC_OWNER':owner}
    def process(environment):
        return subprocess.run([sys.executable,str(root/'services/worker/local_synthetic.py')],env=environment,text=True,capture_output=True,timeout=30,creationflags=hidden)
    result=process(worker_env)
    assert result.returncode==0 and result.stdout.strip()=='SYNTHETIC completed one turn'
    assert query(f"SELECT state FROM companion.turns WHERE id='{rolled}';").strip()=='completed'
    assert query(f"SELECT text FROM companion.messages WHERE turn_id='{rolled}' AND role='assistant';").strip()=='[SYNTHETIC] Local worker lifecycle check.'
    result=process(worker_env)
    assert result.returncode==0 and result.stdout.strip()=='SYNTHETIC idle'
    lines.append('PASS non-owner local worker process completes queued synthetic work then idles')
    assert process({**worker_env,'APP_ENV':'production'}).returncode!=0
    assert process({**worker_env,'PGHOST':'example.invalid'}).returncode!=0
    assert process({**worker_env,'PGUSER':env['PGUSER']}).returncode!=0
    lines.append('PASS worker rejects production, non-loopback and privileged database login')
    return lines
