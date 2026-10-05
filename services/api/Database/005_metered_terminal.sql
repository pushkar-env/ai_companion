-- Trusted database worker entry point. No public HTTP completion route.
BEGIN;
CREATE ROLE companion_worker NOLOGIN NOSUPERUSER NOBYPASSRLS;
GRANT companion_runtime TO companion_worker;
CREATE FUNCTION companion.finish_metered_text(p_turn uuid,p_version bigint,p_state text,p_text text,p_actual bigint)
RETURNS uuid LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); reservation uuid; result uuid;
BEGIN
    IF actor IS NULL THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    -- Preserve owner-first lock ordering shared by admission and settlement.
    PERFORM 1 FROM companion.users WHERE id=actor AND status='active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT reservation_id INTO reservation FROM companion.turns WHERE id=p_turn AND user_id=actor FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='turn_unavailable'; END IF;
    IF reservation IS NULL THEN RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='metered_turn_required'; END IF;
    -- Both functions participate in this statement/transaction. A settlement error also
    -- rolls back reply text, sequence changes and terminal outbox publication.
    result:=companion.finish_text(p_turn,p_version,p_state,p_text);
    PERFORM companion.settle_usage(reservation,p_actual);
    RETURN result;
END $$;
REVOKE ALL ON FUNCTION companion.finish_metered_text(uuid,bigint,text,text,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.finish_metered_text(uuid,bigint,text,text,bigint) TO companion_worker;
INSERT INTO companion.schema_migrations(version) VALUES(5);
COMMIT;
