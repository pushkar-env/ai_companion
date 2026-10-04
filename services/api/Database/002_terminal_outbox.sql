-- Apply after 001 as migration owner. Synthetic/local backend foundation only.
BEGIN;
ALTER TABLE companion.outbox DROP CONSTRAINT outbox_event_type_check;
ALTER TABLE companion.outbox ADD CONSTRAINT outbox_event_type_check
    CHECK(event_type IN ('turn.accepted','turn.completed','turn.cancelled','turn.failed'));
ALTER TABLE companion.outbox ADD UNIQUE(event_id,user_id);
CREATE UNIQUE INDEX one_assistant_message ON companion.messages(turn_id) WHERE role='assistant';
GRANT UPDATE(status) ON companion.messages TO companion_runtime;

CREATE FUNCTION companion.finish_text(p_turn uuid,p_version bigint,p_state text,p_text text DEFAULT NULL)
RETURNS uuid LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); t companion.turns%ROWTYPE; canonical text; seq bigint;
BEGIN
    IF actor IS NULL OR p_version IS NULL OR p_version<1 OR p_state IS NULL
       OR p_state NOT IN ('completed','cancelled','failed')
       OR (p_state='completed' AND (p_text IS NULL OR length(btrim(p_text))=0 OR length(p_text)>8000))
       OR (p_state<>'completed' AND p_text IS NOT NULL) THEN
        RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_terminal_request';
    END IF;
    PERFORM 1 FROM companion.users WHERE id=actor AND status='active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT * INTO t FROM companion.turns WHERE id=p_turn AND user_id=actor FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='turn_unavailable'; END IF;
    IF t.state IN ('completed','cancelled','failed') THEN
        SELECT text INTO canonical FROM companion.messages WHERE turn_id=t.id AND user_id=actor AND role='assistant';
        IF t.state=p_state AND canonical IS NOT DISTINCT FROM p_text AND t.version=p_version+1 THEN RETURN t.id; END IF;
        RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='terminal_conflict';
    END IF;
    IF t.version<>p_version THEN RAISE EXCEPTION USING ERRCODE='40001',MESSAGE='stale_turn_version'; END IF;
    SELECT next_sequence INTO seq FROM companion.conversations WHERE id=t.conversation_id AND user_id=actor FOR UPDATE;
    IF p_state='completed' THEN
        INSERT INTO companion.messages(id,user_id,conversation_id,turn_id,role,text,sequence,status)
            VALUES(gen_random_uuid(),actor,t.conversation_id,t.id,'assistant',p_text,seq,'completed');
    END IF;
    UPDATE companion.messages SET status=p_state WHERE turn_id=t.id AND user_id=actor AND role='user';
    UPDATE companion.turns SET state=p_state,version=version+1,updated_at=now() WHERE id=t.id AND user_id=actor;
    UPDATE companion.conversations SET version=version+1,next_sequence=next_sequence+CASE WHEN p_state='completed' THEN 1 ELSE 0 END
        WHERE id=t.conversation_id AND user_id=actor;
    INSERT INTO companion.outbox(event_id,user_id,conversation_id,turn_id,aggregate_version,event_type)
        VALUES(gen_random_uuid(),actor,t.conversation_id,t.id,t.version+1,'turn.'||p_state);
    RETURN t.id;
END $$;
REVOKE ALL ON FUNCTION companion.finish_text(uuid,bigint,text,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.finish_text(uuid,bigint,text,text) TO companion_runtime;

-- A concrete database-local consumer, not a claim of external exactly-once delivery.
CREATE TABLE companion.consumer_dedupe (
    user_id uuid NOT NULL,
    event_id uuid NOT NULL,
    consumer text NOT NULL CHECK(consumer='turn-status-v1'),
    processed_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id,event_id,consumer),
    FOREIGN KEY(event_id,user_id) REFERENCES companion.outbox(event_id,user_id)
);
CREATE TABLE companion.turn_status_projection (
    turn_id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    version bigint NOT NULL CHECK(version>0),
    state text NOT NULL CHECK(state IN ('accepted','completed','cancelled','failed')),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY(turn_id,user_id,conversation_id) REFERENCES companion.turns(id,user_id,conversation_id)
);
ALTER TABLE companion.consumer_dedupe ENABLE ROW LEVEL SECURITY;
ALTER TABLE companion.consumer_dedupe FORCE ROW LEVEL SECURITY;
CREATE POLICY owner_only ON companion.consumer_dedupe USING(user_id=companion.actor_id()) WITH CHECK(user_id=companion.actor_id());
ALTER TABLE companion.turn_status_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE companion.turn_status_projection FORCE ROW LEVEL SECURITY;
CREATE POLICY owner_only ON companion.turn_status_projection USING(user_id=companion.actor_id()) WITH CHECK(user_id=companion.actor_id());
GRANT SELECT,INSERT ON companion.consumer_dedupe TO companion_runtime;
GRANT SELECT,INSERT,UPDATE ON companion.turn_status_projection TO companion_runtime;
GRANT UPDATE(published_at) ON companion.outbox TO companion_runtime;

CREATE FUNCTION companion.process_next_status_event() RETURNS uuid
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE event companion.outbox%ROWTYPE; inserted integer;
BEGIN
    IF companion.actor_id() IS NULL THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT * INTO event FROM companion.outbox WHERE user_id=companion.actor_id() AND published_at IS NULL
        ORDER BY created_at,event_id LIMIT 1 FOR UPDATE SKIP LOCKED;
    IF NOT FOUND THEN RETURN NULL; END IF;
    INSERT INTO companion.consumer_dedupe(user_id,event_id,consumer)
        VALUES(event.user_id,event.event_id,'turn-status-v1') ON CONFLICT DO NOTHING;
    GET DIAGNOSTICS inserted=ROW_COUNT;
    IF inserted=1 THEN
        INSERT INTO companion.turn_status_projection(turn_id,user_id,conversation_id,version,state)
            VALUES(event.turn_id,event.user_id,event.conversation_id,event.aggregate_version,substring(event.event_type FROM 6))
        ON CONFLICT(turn_id) DO UPDATE SET version=EXCLUDED.version,state=EXCLUDED.state,updated_at=now()
            WHERE companion.turn_status_projection.version<EXCLUDED.version;
    END IF;
    UPDATE companion.outbox SET published_at=now() WHERE event_id=event.event_id AND user_id=event.user_id;
    RETURN event.event_id;
END $$;
REVOKE ALL ON FUNCTION companion.process_next_status_event() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.process_next_status_event() TO companion_runtime;
INSERT INTO companion.schema_migrations(version) VALUES(2);
COMMIT;
