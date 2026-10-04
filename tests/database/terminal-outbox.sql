-- Runs after the admission/crash-recovery fixture. Owners A/B each have one accepted turn.
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE t uuid; second uuid; third uuid; n integer:=0; BEGIN
    SELECT id INTO t FROM companion.turns;
    BEGIN
        PERFORM companion.finish_text(t,99,'completed','Synthetic reply');
        RAISE EXCEPTION 'stale version accepted';
    EXCEPTION WHEN serialization_failure THEN NULL; END;
    PERFORM companion.finish_text(t,1,'completed','Synthetic नमस्ते 🌼 reply');
    PERFORM companion.finish_text(t,1,'completed','Synthetic नमस्ते 🌼 reply');
    IF (SELECT count(*) FROM companion.messages WHERE role='assistant')<>1
       OR (SELECT count(*) FROM companion.outbox WHERE event_type='turn.completed')<>1
       OR (SELECT version FROM companion.turns WHERE id=t)<>2 THEN RAISE EXCEPTION 'completion duplicated'; END IF;
    IF (SELECT sequence FROM companion.messages WHERE role='assistant')<>2 THEN RAISE EXCEPTION 'reply sequence incorrect'; END IF;
    IF (SELECT text FROM companion.messages WHERE role='assistant')<>'Synthetic नमस्ते 🌼 reply' THEN RAISE EXCEPTION 'Unicode reply changed'; END IF;
    BEGIN
        PERFORM companion.finish_text(t,1,'completed','Conflicting reply');
        RAISE EXCEPTION 'completion overwritten';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    BEGIN
        PERFORM companion.finish_text(t,1,'cancelled');
        RAISE EXCEPTION 'completion cancelled later';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    second:=companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000005','40000000-0000-0000-0000-000000000005','Synthetic cancellation');
    PERFORM companion.finish_text(second,1,'cancelled');
    PERFORM companion.finish_text(second,1,'cancelled');
    BEGIN
        PERFORM companion.finish_text(second,1,'completed','Late worker reply');
        RAISE EXCEPTION 'cancelled turn resurrected';
    EXCEPTION WHEN unique_violation THEN NULL; END;
    third:=companion.accept_text('20000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000006','40000000-0000-0000-0000-000000000006','Synthetic failure');
    PERFORM companion.finish_text(third,1,'failed');
    IF EXISTS(SELECT 1 FROM companion.messages WHERE turn_id IN(second,third) AND role='assistant') THEN RAISE EXCEPTION 'invented terminal text'; END IF;
    IF (SELECT count(*) FROM companion.messages WHERE status IN('cancelled','failed'))<>2 THEN RAISE EXCEPTION 'user message state mismatch'; END IF;
    IF (SELECT next_sequence FROM companion.conversations)<>5 THEN RAISE EXCEPTION 'sequence consumed without message'; END IF;
END $$;
COMMIT;
\echo PASS completion persists one canonical sequenced reply and terminal event
\echo PASS Hindi and emoji text round-trips exactly through PostgreSQL
\echo PASS stale version, changed retry and conflicting terminal transition rejected
\echo PASS cancel and failure release active turn without inventing assistant text

RESET ROLE;
SELECT set_config('companion.test_other_turn',(SELECT id::text FROM companion.turns WHERE user_id='00000000-0000-0000-0000-000000000001' AND state='completed'),false);
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
DO $$ BEGIN
    BEGIN
        PERFORM companion.finish_text(current_setting('companion.test_other_turn')::uuid,1,'cancelled');
        RAISE EXCEPTION 'cross-owner finish allowed';
    EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    IF EXISTS(SELECT 1 FROM companion.messages WHERE role='assistant') THEN RAISE EXCEPTION 'reply leaked to other owner'; END IF;
END $$;
ROLLBACK;
RESET ROLE;
\echo PASS canonical reply isolated from other owner

-- Trigger fault simulates a consumer failing after inserting its dedupe key.
CREATE FUNCTION companion.test_consumer_fault() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'synthetic_consumer_failure'; END $$;
CREATE TRIGGER test_fault BEFORE INSERT ON companion.turn_status_projection
    FOR EACH ROW EXECUTE FUNCTION companion.test_consumer_fault();
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ BEGIN
    BEGIN
        PERFORM companion.process_next_status_event();
        RAISE EXCEPTION 'expected consumer failure absent';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM<>'synthetic_consumer_failure' THEN RAISE; END IF;
    END;
    IF EXISTS(SELECT 1 FROM companion.consumer_dedupe) OR EXISTS(SELECT 1 FROM companion.outbox WHERE published_at IS NOT NULL)
       OR EXISTS(SELECT 1 FROM companion.turn_status_projection) THEN RAISE EXCEPTION 'failed consumer committed partial effects'; END IF;
END $$;
COMMIT;
RESET ROLE;
DROP TRIGGER test_fault ON companion.turn_status_projection;
DROP FUNCTION companion.test_consumer_fault();
\echo PASS consumer fault rolls back dedupe effect and acknowledgement together

SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE n integer:=0; BEGIN
    WHILE companion.process_next_status_event() IS NOT NULL LOOP
        n:=n+1; IF n>10 THEN RAISE EXCEPTION 'unbounded delivery'; END IF;
    END LOOP;
    IF n<>6 OR (SELECT count(*) FROM companion.consumer_dedupe)<>6
       OR (SELECT count(*) FROM companion.turn_status_projection)<>3 THEN RAISE EXCEPTION 'delivery missing/duplicated'; END IF;
    IF EXISTS(SELECT 1 FROM companion.turn_status_projection p JOIN companion.turns t ON t.id=p.turn_id WHERE p.state<>t.state OR p.version<>t.version) THEN RAISE EXCEPTION 'out-of-order event regressed projection'; END IF;
END $$;
COMMIT;
RESET ROLE;
CREATE TEMP TABLE prior_projection AS SELECT * FROM companion.turn_status_projection;
-- Redeliver already-consumed events, including older accepted versions.
UPDATE companion.outbox SET published_at=NULL WHERE user_id='00000000-0000-0000-0000-000000000001';
SET ROLE companion_runtime;
BEGIN;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ BEGIN WHILE companion.process_next_status_event() IS NOT NULL LOOP NULL; END LOOP; END $$;
COMMIT;
RESET ROLE;
DO $$ BEGIN
    IF EXISTS((SELECT * FROM companion.turn_status_projection EXCEPT SELECT * FROM prior_projection)
        UNION ALL (SELECT * FROM prior_projection EXCEPT SELECT * FROM companion.turn_status_projection)) THEN RAISE EXCEPTION 'duplicate delivery changed effects'; END IF;
    IF (SELECT count(*) FROM companion.consumer_dedupe)<>6 THEN RAISE EXCEPTION 'duplicate receipt'; END IF;
    IF EXISTS(SELECT 1 FROM companion.outbox WHERE user_id='00000000-0000-0000-0000-000000000002' AND published_at IS NOT NULL) THEN RAISE EXCEPTION 'consumer crossed owner scope'; END IF;
END $$;
\echo PASS bounded owner-scoped consumer catches up without regressing terminal status
\echo PASS redelivery is deduplicated and cannot change prior projection effects
