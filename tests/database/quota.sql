-- Test-only units/caps, not product prices or approved free allowances.
INSERT INTO companion.global_budgets(id,unit_type,limit_units,starts_at,ends_at)
VALUES('50000000-0000-0000-0000-000000000001','synthetic_units',100,now()-interval '1 day',now()+interval '1 day');
INSERT INTO companion.user_budgets(user_id,budget_id,limit_units) VALUES
('00000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001',60),
('00000000-0000-0000-0000-000000000002','50000000-0000-0000-0000-000000000001',60);
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE id uuid; again uuid; deadline timestamptz:=now()+interval '1 hour'; BEGIN
    id:=companion.reserve_usage('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000001',40,deadline);
    again:=companion.reserve_usage('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000001',40,deadline);
    IF id<>again OR (SELECT held_units FROM companion.user_budgets)<>40 THEN RAISE EXCEPTION 'reservation duplicated'; END IF;
    BEGIN
        PERFORM companion.reserve_usage('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000001',41,deadline);
        RAISE EXCEPTION 'changed reservation accepted';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    BEGIN
        PERFORM companion.reserve_usage('50000000-0000-0000-0000-000000000001',gen_random_uuid(),21,deadline);
        RAISE EXCEPTION 'account overspent';
    EXCEPTION WHEN raise_exception THEN IF SQLERRM<>'quota_exceeded' THEN RAISE; END IF; END;
    BEGIN
        PERFORM companion.settle_usage(id,41);
        RAISE EXCEPTION 'unfunded usage accepted';
    EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
    PERFORM companion.settle_usage(id,25);
    PERFORM companion.settle_usage(id,25);
    IF (SELECT count(*) FROM companion.usage_ledger)<>1 OR (SELECT spent_units FROM companion.user_budgets)<>25
       OR (SELECT held_units FROM companion.user_budgets)<>0 THEN RAISE EXCEPTION 'settlement incorrect'; END IF;
    BEGIN
        PERFORM companion.settle_usage(id,24);
        RAISE EXCEPTION 'settlement changed';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    BEGIN
        UPDATE companion.usage_ledger SET units=0;
        RAISE EXCEPTION 'ledger editable';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    BEGIN
        UPDATE companion.user_budgets SET limit_units=999;
        RAISE EXCEPTION 'runtime raised allowance';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
COMMIT;
\echo PASS duplicate reservations and settlements do not double count
\echo PASS account cap, changed keys, unfunded usage and changed settlement rejected
\echo PASS partial settlement releases remainder and ledger is append-only to runtime
\echo PASS runtime cannot raise account allowance
RESET ROLE;
SELECT set_config('companion.test_other_reservation',(SELECT id::text FROM companion.usage_reservations),false);
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
DO $$ DECLARE id uuid; BEGIN
    IF EXISTS(SELECT 1 FROM companion.usage_reservations) OR EXISTS(SELECT 1 FROM companion.usage_ledger) THEN RAISE EXCEPTION 'quota data leaked'; END IF;
    BEGIN
        PERFORM companion.settle_usage(current_setting('companion.test_other_reservation')::uuid,25);
        RAISE EXCEPTION 'other owner settled';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    id:=companion.reserve_usage('50000000-0000-0000-0000-000000000001',gen_random_uuid(),20,now()+interval '1 hour');
    PERFORM companion.settle_usage(id,0);
    PERFORM companion.settle_usage(id,0);
    IF (SELECT held_units+spent_units FROM companion.user_budgets)<>0 THEN RAISE EXCEPTION 'release charged units'; END IF;
END $$;
ROLLBACK;
RESET ROLE;
DO $$ BEGIN
    IF (SELECT spent_units FROM companion.global_budgets)<>25 OR (SELECT held_units FROM companion.global_budgets)<>0
       OR (SELECT count(*) FROM companion.usage_reservations)<>1 THEN RAISE EXCEPTION 'rollback leaked accounting'; END IF;
END $$;
\echo PASS cross-owner quota access denied and zero settlement releases without charging
\echo PASS rollback restores reservations ledger and both budget balances

SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ BEGIN
    BEGIN
        PERFORM companion.reserve_usage('50000000-0000-0000-0000-000000000001',gen_random_uuid(),1,now()-interval '1 second');
        RAISE EXCEPTION 'expired reservation admitted';
    EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
    BEGIN
        PERFORM companion.reserve_usage('50000000-0000-0000-0000-000000000001',gen_random_uuid(),1,now()+interval '2 days');
        RAISE EXCEPTION 'reservation exceeded period';
    EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
    BEGIN
        PERFORM companion.reserve_usage('50000000-0000-0000-0000-000000000001',gen_random_uuid(),10,now()+interval '1 hour');
        -- Failure after reserving must roll back the hold and all admission writes.
        PERFORM companion.accept_text('20000000-0000-0000-0000-000000000002',gen_random_uuid(),gen_random_uuid(),'Synthetic rejected transaction');
        RAISE EXCEPTION 'unauthorized admission accepted';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    IF (SELECT held_units FROM companion.user_budgets)<>0 OR (SELECT count(*) FROM companion.usage_reservations)<>1 THEN RAISE EXCEPTION 'admission failure stranded hold'; END IF;
END $$;
COMMIT;
RESET ROLE;
\echo PASS invalid reservation deadlines rejected
\echo PASS combined reserve and rejected admission transaction strands no hold

-- A separate period exercises global cap with multiple owners simultaneously.
INSERT INTO companion.global_budgets(id,unit_type,limit_units,starts_at,ends_at)
VALUES('50000000-0000-0000-0000-000000000002','synthetic_units',50,now()-interval '1 day',now()+interval '1 day');
INSERT INTO companion.user_budgets(user_id,budget_id,limit_units) VALUES
('00000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000002',100),
('00000000-0000-0000-0000-000000000002','50000000-0000-0000-0000-000000000002',100);
