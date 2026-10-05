"""Disposable local PostgreSQL integration tests; never connects to an existing database.

Requires existing PostgreSQL 18 binaries via PG_BIN (Windows install detected by default).
Uses a random local password and port, synthetic data, and leaves stopped artifacts for
inspection. No credentials or database content are printed or written into source control.
"""
import concurrent.futures
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import sys
import importlib.util

ROOT = Path(__file__).resolve().parents[1]
BIN = Path(os.environ.get('PG_BIN', 'C:/Program Files/PostgreSQL/18/bin'))
SUFFIX = '.exe' if os.name == 'nt' else ''
HIDDEN = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
for tool in ('initdb', 'pg_ctl', 'psql', 'postgres'):
    if not (BIN / (tool + SUFFIX)).is_file():
        raise SystemExit('BLOCKED: set PG_BIN to an approved PostgreSQL 18 bin directory')
version = subprocess.check_output([str(BIN / ('postgres'+SUFFIX)), '--version'], text=True, creationflags=HIDDEN).strip()
if ' 18.' not in version:
    raise SystemExit('BLOCKED: this test is validated for PostgreSQL 18')
base = ROOT / 'artifacts/database-tests'
base.mkdir(parents=True, exist_ok=True)
work = Path(tempfile.mkdtemp(prefix='run-', dir=base))
data = work / 'data'
password = secrets.token_urlsafe(32)
pwfile = work / 'password.txt'
pwfile.write_text(password, encoding='utf-8')
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
env = {**os.environ, 'PGPASSWORD': password, 'PGHOST': '127.0.0.1', 'PGPORT': str(port), 'PGUSER': 'companion_test_admin', 'PGDATABASE': 'postgres', 'PGCONNECT_TIMEOUT': '5', 'PGCLIENTENCODING': 'UTF8'}
evidence = ROOT / 'docs/evidence/m2/database'
evidence.mkdir(parents=True, exist_ok=True)
lines = [version]

def run(tool, args, sql=None):
    # pg_ctl's detached Windows server can inherit pipes and keep communicate() open.
    # File-backed output avoids that hang and works for concurrent psql sessions too.
    with tempfile.TemporaryFile(mode='w+', encoding='utf-8') as stdout, tempfile.TemporaryFile(mode='w+', encoding='utf-8') as stderr:
        result = subprocess.run([str(BIN/(tool+SUFFIX)), *args], input=sql, text=True, encoding='utf-8',
                                stdout=stdout, stderr=stderr, env=env, creationflags=HIDDEN, timeout=90)
        stdout.seek(0); stderr.seek(0)
        output, errors = stdout.read(), stderr.read()
    if result.returncode:
        raise RuntimeError((errors or output).replace(password, '[redacted]')[-2500:])
    return output

def query(sql):
    return run('psql', ['-X', '-q', '-A', '-t', '-v', 'ON_ERROR_STOP=1'], sql)

def start():
    run('pg_ctl', ['-D', str(data), '-l', str(work/'server.log'), '-w', 'start'])

def stop(mode='fast'):
    run('pg_ctl', ['-D', str(data), '-m', mode, '-w', 'stop'])

started = False
try:
    run('initdb', ['-D', str(data), '-U', 'companion_test_admin', '-A', 'scram-sha-256', '--pwfile', str(pwfile), '--encoding=UTF8', '--no-locale'])
    with (data/'postgresql.conf').open('a', encoding='utf-8') as out:
        out.write(f"\nlisten_addresses='127.0.0.1'\nport={port}\nlog_statement='none'\n")
    start(); started = True
    query('CREATE ROLE companion_runtime NOLOGIN NOSUPERUSER NOBYPASSRLS;')
    query((ROOT/'services/api/Database/001_conversations.sql').read_text(encoding='utf-8'))
    output = query((ROOT/'tests/database/conversations.sql').read_text(encoding='utf-8'))
    lines += [line for line in output.splitlines() if line.startswith('PASS ')]
    # Two real concurrent sessions submit the same request for owner B.
    admission = """BEGIN; SET LOCAL ROLE companion_runtime;
    SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
    SELECT companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','Synthetic concurrent request');
    SELECT pg_sleep(0.2); COMMIT;"""
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        a, b = list(pool.map(query, [admission, admission]))
    assert a == b, 'Concurrent replay produced different IDs'
    assert query('SELECT count(*) FROM companion.turns;').strip() == '2'
    assert query('SELECT count(*) FROM companion.outbox;').strip() == '2'
    lines.append('PASS concurrent duplicate sessions produce one turn and one outbox event')
    snapshot = query('SELECT id FROM companion.turns ORDER BY id;')
    stop('immediate'); started = False
    start(); started = True
    assert query('SELECT id FROM companion.turns ORDER BY id;') == snapshot
    assert query('SELECT count(*) FROM companion.outbox WHERE published_at IS NULL;').strip() == '2'
    assert query(admission) == a
    lines.append('PASS crash recovery preserves accepted turns, pending outbox and idempotent result')
    query((ROOT/'services/api/Database/002_terminal_outbox.sql').read_text(encoding='utf-8'))
    output = query((ROOT/'tests/database/terminal-outbox.sql').read_text(encoding='utf-8'))
    lines += [line for line in output.splitlines() if line.startswith('PASS ')]
    # Competing completion and cancellation must choose one terminal result.
    turn_b = query("SELECT id FROM companion.turns WHERE user_id='00000000-0000-0000-0000-000000000002';").strip()
    def finish(state):
        text = "'Synthetic racing reply'" if state == 'completed' else 'NULL'
        sql = f"""BEGIN; SET LOCAL ROLE companion_runtime;
        SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
        SELECT companion.finish_text('{turn_b}',1,'{state}',{text}); COMMIT;"""
        try:
            query(sql)
            return 'won'
        except RuntimeError as error:
            if 'terminal_conflict' not in str(error):
                raise
            return 'conflict'
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(finish, ['completed', 'cancelled']))
    assert sorted(results) == ['conflict', 'won']
    assert query(f"SELECT count(*) FROM companion.outbox WHERE turn_id='{turn_b}' AND aggregate_version=2;").strip() == '1'
    state_b = query(f"SELECT state FROM companion.turns WHERE id='{turn_b}';").strip()
    assert query(f"SELECT count(*) FROM companion.messages WHERE turn_id='{turn_b}' AND role='assistant';").strip() == ('1' if state_b == 'completed' else '0')
    lines.append('PASS concurrent completion versus cancellation commits only the winning terminal result')
    consume = """BEGIN; SET LOCAL ROLE companion_runtime;
    SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
    SELECT companion.process_next_status_event(); SELECT pg_sleep(0.1); COMMIT;"""
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(query, [consume, consume]))
    assert query('SELECT count(*) FROM companion.outbox WHERE published_at IS NULL;').strip() == '0'
    assert query('SELECT count(*) FROM companion.consumer_dedupe;').strip() == '8'
    assert query(f"SELECT state FROM companion.turn_status_projection WHERE turn_id='{turn_b}';").strip() == state_b
    lines.append('PASS concurrent consumers acknowledge distinct events and converge on latest version')
    terminal_snapshot = query('SELECT id,state,version FROM companion.turns ORDER BY id;')
    messages_snapshot = query('SELECT id,sequence,status,text FROM companion.messages ORDER BY id;')
    receipts_snapshot = query('SELECT event_id FROM companion.consumer_dedupe ORDER BY event_id;')
    stop('immediate'); started = False
    start(); started = True
    assert query('SELECT id,state,version FROM companion.turns ORDER BY id;') == terminal_snapshot
    assert query('SELECT id,sequence,status,text FROM companion.messages ORDER BY id;') == messages_snapshot
    assert query('SELECT event_id FROM companion.consumer_dedupe ORDER BY event_id;') == receipts_snapshot
    lines.append('PASS crash recovery retains canonical messages terminal states and consumer dedupe')
    query((ROOT/'services/api/Database/003_quota.sql').read_text(encoding='utf-8'))
    output = query((ROOT/'tests/database/quota.sql').read_text(encoding='utf-8'))
    lines += [line for line in output.splitlines() if line.startswith('PASS ')]
    def reserve(index):
        actor = '00000000-0000-0000-0000-00000000000'+str(1+index%2)
        key = '70000000-0000-0000-0000-'+str(index+1).zfill(12)
        sql = f"""BEGIN; SET LOCAL ROLE companion_runtime;
        SELECT set_config('companion.user_id','{actor}',true);
        SELECT companion.reserve_usage('50000000-0000-0000-0000-000000000002','{key}',10,now()+interval '1 hour'); COMMIT;"""
        try:
            query(sql)
            return 'reserved'
        except RuntimeError as error:
            if 'quota_exceeded' not in str(error):
                raise
            return 'denied'
    with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool:
        results = list(pool.map(reserve, range(10)))
    assert results.count('reserved') == 5 and results.count('denied') == 5
    assert query("SELECT held_units FROM companion.global_budgets WHERE id='50000000-0000-0000-0000-000000000002';").strip() == '50'
    assert query("SELECT sum(held_units) FROM companion.user_budgets WHERE budget_id='50000000-0000-0000-0000-000000000002';").strip() == '50'
    lines.append('PASS ten concurrent requests across two accounts cannot overspend global cap')
    reservation, actor = query("SELECT id,user_id FROM companion.usage_reservations WHERE state='reserved' ORDER BY id LIMIT 1;").strip().split('|')
    settlement = f"""BEGIN; SET LOCAL ROLE companion_runtime;
    SELECT set_config('companion.user_id','{actor}',true);
    SELECT companion.settle_usage('{reservation}',6); COMMIT;"""
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(query, [settlement, settlement]))
    assert query(f"SELECT count(*) FROM companion.usage_ledger WHERE reservation_id='{reservation}';").strip() == '1'
    assert query("SELECT held_units,spent_units FROM companion.global_budgets WHERE id='50000000-0000-0000-0000-000000000002';").strip() == '40|6'
    lines.append('PASS concurrent settlement charges actual units once and releases unused hold')
    balance_snapshot = query('SELECT id,held_units,spent_units FROM companion.global_budgets ORDER BY id;')
    ledger_snapshot = query('SELECT reservation_id,units FROM companion.usage_ledger ORDER BY reservation_id;')
    stop('immediate'); started = False
    start(); started = True
    assert query('SELECT id,held_units,spent_units FROM companion.global_budgets ORDER BY id;') == balance_snapshot
    assert query('SELECT reservation_id,units FROM companion.usage_ledger ORDER BY reservation_id;') == ledger_snapshot
    query(settlement)
    assert query('SELECT id,held_units,spent_units FROM companion.global_budgets ORDER BY id;') == balance_snapshot
    lines.append('PASS crash recovery retains quota holds and ledger without retry double charge')
    query((ROOT/'services/api/Database/004_metered_admission.sql').read_text(encoding='utf-8'))
    query((ROOT/'services/api/Database/005_metered_terminal.sql').read_text(encoding='utf-8'))
    output=query((ROOT/'tests/database/metered-terminal.sql').read_text(encoding='utf-8'))
    lines += [line for line in output.splitlines() if line.startswith('PASS ')]
    worker_actor="BEGIN; SET LOCAL ROLE companion_worker; SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);"
    metered_turn=query(worker_actor+"SELECT companion.admit_metered_text('20000000-0000-0000-0000-000000000005',gen_random_uuid(),gen_random_uuid(),'Synthetic atomic concurrency','50000000-0000-0000-0000-000000000005',10); COMMIT;").strip().splitlines()[-1]
    completion=worker_actor+f"SELECT companion.finish_metered_text('{metered_turn}',1,'completed','Synthetic atomic reply',4); COMMIT;"
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results=list(pool.map(query,[completion,completion]))
    assert results[0]==results[1]
    assert query(f"SELECT count(*) FROM companion.outbox WHERE turn_id='{metered_turn}' AND event_type='turn.completed';").strip()=='1'
    assert query("SELECT held_units,spent_units FROM companion.global_budgets WHERE id='50000000-0000-0000-0000-000000000005';").strip()=='0|13'
    lines.append('PASS concurrent metered completions publish and charge once')
    atomic_snapshot=query("SELECT id,state,version,reservation_id FROM companion.turns ORDER BY id;")+query('SELECT reservation_id,units FROM companion.usage_ledger ORDER BY reservation_id;')
    stop('immediate'); started=False
    start(); started=True
    query(completion)
    assert query("SELECT id,state,version,reservation_id FROM companion.turns ORDER BY id;")+query('SELECT reservation_id,units FROM companion.usage_ledger ORDER BY reservation_id;')==atomic_snapshot
    assert query("SELECT held_units,spent_units FROM companion.global_budgets WHERE id='50000000-0000-0000-0000-000000000005';").strip()=='0|13'
    lines.append('PASS crash recovery and completion redelivery preserve atomic usage and reply')
    query((ROOT/'services/api/Database/006_worker_leases.sql').read_text(encoding='utf-8'))
    worker_spec=importlib.util.spec_from_file_location('worker_checks',ROOT/'tests/database/check_worker.py')
    worker_module=importlib.util.module_from_spec(worker_spec); worker_spec.loader.exec_module(worker_module)
    lines += worker_module.run(ROOT,query,env,password,HIDDEN)
    query((ROOT/'services/api/Database/007_event_replay.sql').read_text(encoding='utf-8'))
    assert query("SELECT count(*) FROM companion.conversations c WHERE c.next_event_sequence<>(SELECT count(*)+1 FROM companion.outbox e WHERE e.conversation_id=c.id);").strip()=='0'
    assert query("SELECT count(*) FROM companion.outbox a JOIN companion.outbox b ON a.turn_id=b.turn_id WHERE a.aggregate_version<b.aggregate_version AND a.event_sequence>=b.event_sequence;").strip()=='0'
    lines.append('PASS replay migration backfills contiguous cursors with accepted events before terminal events')
    if '--api' in sys.argv:
        module_spec=importlib.util.spec_from_file_location('account_api_checks',ROOT/'tests/e2e/check_account_api.py')
        module=importlib.util.module_from_spec(module_spec); module_spec.loader.exec_module(module)
        lines += module.run(ROOT,work,port,password,query,HIDDEN)
    replay_snapshot=query('SELECT event_id,conversation_id,event_sequence FROM companion.outbox ORDER BY conversation_id,event_sequence;')
    counters_snapshot=query('SELECT id,next_event_sequence FROM companion.conversations ORDER BY id;')
    stop('immediate'); started=False
    start(); started=True
    assert query('SELECT event_id,conversation_id,event_sequence FROM companion.outbox ORDER BY conversation_id,event_sequence;')==replay_snapshot
    assert query('SELECT id,next_event_sequence FROM companion.conversations ORDER BY id;')==counters_snapshot
    lines.append('PASS database crash recovery retains replay events and conversation cursors')
except Exception as error:
    lines.append('FAIL '+str(error))
    raise
finally:
    try:
        if started:
            stop()
    finally:
        pwfile.unlink(missing_ok=True)
        (evidence/'checks.txt').write_text('\n'.join(lines)+'\n', encoding='utf-8')
        print('\n'.join(lines))
        print('Stopped test cluster artifacts: '+str(work))
