"""One bounded synthetic worker pass. No AI/provider calls; no transcript output.
Uses installed psql and inherited local credentials (never prints them).
"""
import os
from pathlib import Path
import subprocess
import uuid


def main():
    if os.environ.get('APP_ENV') != 'local' or os.environ.get('SYNTHETIC_ACCOUNTS_ONLY') != 'true':
        raise ValueError('synthetic_local_mode_required')
    if os.environ.get('PGHOST') != '127.0.0.1':
        raise ValueError('loopback_database_required')
    owner = str(uuid.UUID(os.environ['COMPANION_SYNTHETIC_OWNER']))
    worker = str(uuid.uuid4())
    binary = Path(os.environ.get('PG_BIN', 'C:/Program Files/PostgreSQL/18/bin')) / ('psql.exe' if os.name == 'nt' else 'psql')
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0

    def query(sql):
        prefix = f"BEGIN; SET LOCAL ROLE companion_worker; SELECT set_config('companion.user_id','{owner}',true);"
        result = subprocess.run([str(binary), '-X', '-q', '-A', '-t', '-v', 'ON_ERROR_STOP=1'],
                                input=prefix+sql+' COMMIT;', text=True, encoding='utf-8',
                                capture_output=True, timeout=15, creationflags=flags)
        if result.returncode:
            raise RuntimeError('worker_database_operation_failed')
        return result.stdout.strip().splitlines()[1:]

    # Reject owner/superuser credentials: the harness uses a dedicated non-owner login.
    check = query("SELECT rolsuper OR rolbypassrls OR pg_has_role(session_user,(SELECT tableowner FROM pg_tables WHERE schemaname='companion' AND tablename='turns'),'MEMBER') FROM pg_roles WHERE rolname=session_user;")
    if check != ['f']:
        raise ValueError('non_owner_worker_login_required')
    claimed = query(f"SELECT * FROM companion.claim_local_turn('{worker}',60);")
    if not claimed:
        print('SYNTHETIC idle')
        return
    turn, token, version = claimed[0].split('|')
    turn, token, version = str(uuid.UUID(turn)), str(uuid.UUID(token)), int(version)
    # Fixed labeled response and zero provider usage: this adapter performs no paid work.
    query(f"SELECT companion.finish_local_turn('{turn}','{token}',{version},'completed','[SYNTHETIC] Local worker lifecycle check.',0);")
    print('SYNTHETIC completed one turn')


if __name__ == '__main__':
    try:
        main()
    except Exception:
        raise SystemExit('Worker stopped safely; check local configuration or retry after lease expiry.')
