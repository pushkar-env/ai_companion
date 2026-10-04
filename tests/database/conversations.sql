-- Synthetic data only. Run on the disposable cluster created by check-database.py.
INSERT INTO companion.users(id,locale) VALUES
('00000000-0000-0000-0000-000000000001','en-IN'),
('00000000-0000-0000-0000-000000000002','hi-IN');
INSERT INTO companion.companions(id,user_id,name) VALUES
('10000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','Synthetic A'),
('10000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000002','Synthetic B');
INSERT INTO companion.conversations(id,user_id,companion_id) VALUES
('20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001'),
('20000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002');

SET ROLE companion_runtime;
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM companion.users) OR EXISTS(SELECT 1 FROM companion.conversations) THEN RAISE EXCEPTION 'missing actor leaked rows'; END IF;
END $$;
\echo PASS absent actor fails closed
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE turn_id uuid; again uuid; BEGIN
    IF (SELECT count(*) FROM companion.users)<>1 OR (SELECT count(*) FROM companion.companions)<>1
       OR (SELECT count(*) FROM companion.conversations)<>1 THEN RAISE EXCEPTION 'owner isolation failed'; END IF;
    turn_id:=companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','Synthetic hello');
    again:=companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','Synthetic hello');
    IF turn_id<>again THEN RAISE EXCEPTION 'duplicate request'; END IF;
    again:=companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000009','40000000-0000-0000-0000-000000000001','Synthetic hello');
    IF turn_id<>again OR (SELECT count(*) FROM companion.messages)<>1 OR (SELECT count(*) FROM companion.outbox)<>1 THEN RAISE EXCEPTION 'duplicate client message'; END IF;
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','Changed synthetic payload');
        RAISE EXCEPTION 'conflicting key accepted';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','Cross-owner attempt');
        RAISE EXCEPTION 'cross-owner admission accepted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        INSERT INTO companion.conversations(id,user_id,companion_id) VALUES(gen_random_uuid(),'00000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000002');
        RAISE EXCEPTION 'cross-owner parent accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL; END;
    BEGIN
        INSERT INTO companion.conversations(id,user_id,companion_id) VALUES(gen_random_uuid(),'00000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002');
        RAISE EXCEPTION 'RLS insert accepted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000003','40000000-0000-0000-0000-000000000003','Concurrent second turn');
        RAISE EXCEPTION 'second active turn accepted';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    IF (SELECT count(*) FROM companion.messages)<>1 OR (SELECT count(*) FROM companion.outbox)<>1
       OR (SELECT next_sequence FROM companion.conversations)<>2 THEN RAISE EXCEPTION 'failed acceptance left partial writes'; END IF;
END $$;
COMMIT;
\echo PASS owner-scoped reads and atomic idempotent admission
\echo PASS conflicting keys and active turn rejected without partial writes
\echo PASS cross-owner admission, child links and inserts rejected
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM companion.messages) OR EXISTS(SELECT 1 FROM companion.users) THEN RAISE EXCEPTION 'transaction actor leaked'; END IF;
END $$;
\echo PASS actor cleared on pooled connection after commit
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM companion.messages) OR EXISTS(SELECT 1 FROM companion.outbox) THEN RAISE EXCEPTION 'other owner leaked accepted data'; END IF;
    PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','Synthetic rollback');
END $$;
ROLLBACK;
RESET ROLE;
DO $$ BEGIN
    IF (SELECT count(*) FROM companion.turns)<>1 OR (SELECT count(*) FROM companion.messages)<>1
       OR (SELECT count(*) FROM companion.outbox)<>1 OR (SELECT count(*) FROM companion.idempotency_requests)<>2
       THEN RAISE EXCEPTION 'rollback left durable partial writes'; END IF;
    IF (SELECT rolbypassrls OR rolsuper FROM pg_roles WHERE rolname='companion_runtime') THEN RAISE EXCEPTION 'runtime bypass'; END IF;
    IF EXISTS(SELECT 1 FROM pg_tables WHERE schemaname='companion' AND tableowner='companion_runtime') THEN RAISE EXCEPTION 'runtime owns tables'; END IF;
END $$;
\echo PASS rollback removes turn message outbox and key together
\echo PASS runtime role neither owns tables nor bypasses RLS

BEGIN;
UPDATE companion.users SET status='deleting' WHERE id='00000000-0000-0000-0000-000000000002';
SET LOCAL ROLE companion_runtime;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
DO $$ BEGIN
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','Synthetic deleting owner');
        RAISE EXCEPTION 'deleting user accepted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        UPDATE companion.users SET status='active';
        RAISE EXCEPTION 'runtime reactivated user';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
ROLLBACK;
\echo PASS deleting owner cannot admit turns or reactivate through runtime role

BEGIN;
SET LOCAL ROLE companion_runtime;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
DO $$ BEGIN
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002',repeat('x',8001));
        RAISE EXCEPTION 'oversize accepted';
    EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','   ');
        RAISE EXCEPTION 'blank accepted';
    EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
    UPDATE companion.conversations SET state='closed';
    BEGIN
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002','Closed synthetic conversation');
        RAISE EXCEPTION 'closed conversation accepted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
ROLLBACK;
SET ROLE companion_runtime;
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM companion.users) THEN RAISE EXCEPTION 'actor survived rollback'; END IF;
END $$;
RESET ROLE;
\echo PASS bounded input and closed conversations reject admission
\echo PASS rollback clears actor and restores runtime role on reused connection
