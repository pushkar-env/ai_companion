-- Synthetic accounting units, never prices or a cancellation charging policy.
INSERT INTO companion.global_budgets(id,unit_type,limit_units,starts_at,ends_at)
VALUES('50000000-0000-0000-0000-000000000005','synthetic_units',100,now()-interval '1 day',now()+interval '1 day');
INSERT INTO companion.user_budgets(user_id,budget_id,limit_units)
VALUES('00000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000005',100);
INSERT INTO companion.conversations(id,user_id,companion_id)
VALUES('20000000-0000-0000-0000-000000000005','00000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001');
DO $$ BEGIN
 IF has_function_privilege('companion_runtime','companion.finish_metered_text(uuid,bigint,text,text,bigint)','EXECUTE') THEN RAISE EXCEPTION 'account role can invoke worker entry'; END IF;
END $$;
\echo PASS metered completion entry restricted to worker role
BEGIN;
SET LOCAL ROLE companion_worker;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE test_turn uuid; hold uuid; BEGIN
 test_turn:=companion.admit_metered_text('20000000-0000-0000-0000-000000000005',gen_random_uuid(),gen_random_uuid(),'Synthetic metered turn','50000000-0000-0000-0000-000000000005',10);
 SELECT reservation_id INTO hold FROM companion.turns WHERE turns.id=test_turn;
 BEGIN
  PERFORM companion.finish_metered_text(test_turn,1,'completed','Synthetic answer',11);
  RAISE EXCEPTION 'over-budget accepted';
 EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
 IF (SELECT turns.state FROM companion.turns WHERE turns.id=test_turn)<>'accepted'
 OR EXISTS(SELECT 1 FROM companion.messages WHERE turn_id=test_turn AND role='assistant')
 OR EXISTS(SELECT 1 FROM companion.outbox WHERE turn_id=test_turn AND event_type='turn.completed')
 OR EXISTS(SELECT 1 FROM companion.usage_ledger WHERE reservation_id=hold) THEN RAISE EXCEPTION 'partial terminal write survived'; END IF;
 PERFORM companion.finish_metered_text(test_turn,1,'completed','Synthetic answer',6);
 PERFORM companion.finish_metered_text(test_turn,1,'completed','Synthetic answer',6);
 IF (SELECT count(*) FROM companion.messages WHERE turn_id=test_turn AND role='assistant')<>1
 OR (SELECT count(*) FROM companion.outbox WHERE turn_id=test_turn AND event_type='turn.completed')<>1
 OR (SELECT units FROM companion.usage_ledger WHERE reservation_id=hold)<>6
 OR (SELECT held_units FROM companion.user_budgets WHERE budget_id='50000000-0000-0000-0000-000000000005')<>0 THEN RAISE EXCEPTION 'retry accounting wrong'; END IF;
 BEGIN
  PERFORM companion.finish_metered_text(test_turn,1,'completed','Synthetic answer',5);
  RAISE EXCEPTION 'usage change accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 BEGIN
  PERFORM companion.finish_metered_text(test_turn,1,'completed','Changed answer',6);
  RAISE EXCEPTION 'text change accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 PERFORM set_config('companion.user_id','00000000-0000-0000-0000-000000000002',true);
 BEGIN
  PERFORM companion.finish_metered_text(test_turn,1,'completed','Synthetic answer',6);
  RAISE EXCEPTION 'cross-owner completion accepted';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
COMMIT;
\echo PASS settlement failure rolls back reply and terminal event
\echo PASS exact terminal retry charges once and releases unused hold
\echo PASS changed usage or reply rejected
\echo PASS worker completion remains owner scoped
BEGIN;
SET LOCAL ROLE companion_worker;
SELECT set_config('companion.user_id','00000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE test_turn uuid; target text; usage bigint; BEGIN
 FOREACH target IN ARRAY ARRAY['cancelled','failed'] LOOP
  usage:=CASE WHEN target='cancelled' THEN 0 ELSE 3 END;
  test_turn:=companion.admit_metered_text('20000000-0000-0000-0000-000000000005',gen_random_uuid(),gen_random_uuid(),'Synthetic terminal test','50000000-0000-0000-0000-000000000005',10);
  PERFORM companion.finish_metered_text(test_turn,1,target,NULL,usage);
  PERFORM companion.finish_metered_text(test_turn,1,target,NULL,usage);
  IF EXISTS(SELECT 1 FROM companion.messages WHERE turn_id=test_turn AND role='assistant') THEN RAISE EXCEPTION 'failed/cancelled answer persisted'; END IF;
 END LOOP;
 IF (SELECT spent_units FROM companion.user_budgets WHERE budget_id='50000000-0000-0000-0000-000000000005')<>9 THEN RAISE EXCEPTION 'terminal usage wrong'; END IF;
END $$;
COMMIT;
\echo PASS cancellation and failure settle explicitly reported usage without assistant text
