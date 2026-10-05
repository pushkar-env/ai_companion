-- Local synthetic worker lifecycle; external provider reconciliation is not enabled.
BEGIN;
CREATE TABLE companion.worker_leases (
 turn_id uuid PRIMARY KEY,
 user_id uuid NOT NULL,
 conversation_id uuid NOT NULL,
 worker_id uuid NOT NULL,
 token uuid NOT NULL,
 expires_at timestamptz NOT NULL,
 finished boolean NOT NULL DEFAULT false,
 FOREIGN KEY(turn_id,user_id,conversation_id) REFERENCES companion.turns(id,user_id,conversation_id)
);
ALTER TABLE companion.worker_leases ENABLE ROW LEVEL SECURITY;
ALTER TABLE companion.worker_leases FORCE ROW LEVEL SECURITY;
CREATE POLICY owner_only ON companion.worker_leases USING(user_id=companion.actor_id()) WITH CHECK(user_id=companion.actor_id());
GRANT SELECT,INSERT,UPDATE ON companion.worker_leases TO companion_worker;
CREATE FUNCTION companion.claim_local_turn(p_worker uuid,p_seconds integer)
RETURNS TABLE(turn_id uuid,lease_token uuid,turn_version bigint)
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); picked companion.turns%ROWTYPE; issued uuid:=gen_random_uuid();
BEGIN
 IF p_worker IS NULL OR p_seconds IS NULL OR p_seconds<1 OR p_seconds>300 THEN
  RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_lease'; END IF;
 PERFORM 1 FROM companion.users WHERE id=actor AND status='active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
 SELECT t.* INTO picked FROM companion.turns t JOIN companion.usage_reservations r ON r.id=t.reservation_id AND r.user_id=actor
 WHERE t.user_id=actor AND t.state='accepted' AND r.state='reserved'
 AND NOT EXISTS(SELECT 1 FROM companion.worker_leases l WHERE l.turn_id=t.id AND (l.finished OR l.expires_at>clock_timestamp()))
 ORDER BY t.created_at,t.id LIMIT 1 FOR UPDATE OF t SKIP LOCKED;
 IF NOT FOUND THEN RETURN; END IF;
 INSERT INTO companion.worker_leases AS l(turn_id,user_id,conversation_id,worker_id,token,expires_at)
 VALUES(picked.id,actor,picked.conversation_id,p_worker,issued,clock_timestamp()+make_interval(secs=>p_seconds))
 ON CONFLICT ON CONSTRAINT worker_leases_pkey DO UPDATE SET worker_id=EXCLUDED.worker_id,token=EXCLUDED.token,expires_at=EXCLUDED.expires_at,finished=false;
 RETURN QUERY SELECT picked.id,issued,picked.version;
END $$;
CREATE FUNCTION companion.renew_local_turn(p_turn uuid,p_token uuid,p_seconds integer) RETURNS boolean
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
BEGIN
 IF p_seconds IS NULL OR p_seconds<1 OR p_seconds>300 THEN RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_lease'; END IF;
 PERFORM 1 FROM companion.users WHERE id=companion.actor_id() AND status='active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
 PERFORM 1 FROM companion.turns WHERE id=p_turn AND user_id=companion.actor_id() AND state='accepted' FOR UPDATE;
 IF NOT FOUND THEN RETURN false; END IF;
 UPDATE companion.worker_leases SET expires_at=clock_timestamp()+make_interval(secs=>p_seconds)
 WHERE turn_id=p_turn AND user_id=companion.actor_id() AND token=p_token AND NOT finished AND expires_at>clock_timestamp();
 RETURN FOUND;
END $$;
CREATE FUNCTION companion.finish_local_turn(p_turn uuid,p_token uuid,p_version bigint,p_state text,p_text text,p_actual bigint) RETURNS uuid
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE lease companion.worker_leases%ROWTYPE; result uuid;
BEGIN
 PERFORM 1 FROM companion.users WHERE id=companion.actor_id() AND status='active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
 PERFORM 1 FROM companion.turns WHERE id=p_turn AND user_id=companion.actor_id() FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='turn_unavailable'; END IF;
 SELECT * INTO lease FROM companion.worker_leases WHERE turn_id=p_turn AND user_id=companion.actor_id() FOR UPDATE;
 IF NOT FOUND OR p_token IS NULL OR lease.token<>p_token OR (NOT lease.finished AND lease.expires_at<=clock_timestamp()) THEN
  RAISE EXCEPTION USING ERRCODE='40001',MESSAGE='lease_lost'; END IF;
 -- Completed receipts may be retried after expiry, but only with the winning token
 -- and the identical version/state/text/units checked by the atomic terminal function.
 result:=companion.finish_metered_text(p_turn,p_version,p_state,p_text,p_actual);
 UPDATE companion.worker_leases SET finished=true WHERE turn_id=p_turn AND user_id=companion.actor_id();
 RETURN result;
END $$;
REVOKE ALL ON FUNCTION companion.claim_local_turn(uuid,integer),companion.renew_local_turn(uuid,uuid,integer),companion.finish_local_turn(uuid,uuid,bigint,text,text,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.claim_local_turn(uuid,integer),companion.renew_local_turn(uuid,uuid,integer),companion.finish_local_turn(uuid,uuid,bigint,text,text,bigint) TO companion_worker;
INSERT INTO companion.schema_migrations(version) VALUES(6);
COMMIT;
