-- Local synthetic history only. Existing events receive reconstructed ordering.
BEGIN;
ALTER TABLE companion.conversations ADD COLUMN next_event_sequence bigint NOT NULL DEFAULT 1 CHECK(next_event_sequence>0);
ALTER TABLE companion.outbox ADD COLUMN event_sequence bigint;
WITH ordered AS (
    SELECT e.event_id,row_number() OVER(PARTITION BY e.conversation_id
        ORDER BY t.created_at,t.id,e.aggregate_version,e.event_id) AS seq
    FROM companion.outbox e JOIN companion.turns t ON t.id=e.turn_id
)
UPDATE companion.outbox e SET event_sequence=o.seq FROM ordered o WHERE e.event_id=o.event_id;
UPDATE companion.conversations c SET next_event_sequence=1+(SELECT count(*) FROM companion.outbox e WHERE e.conversation_id=c.id);
ALTER TABLE companion.outbox ALTER COLUMN event_sequence SET NOT NULL;
ALTER TABLE companion.outbox ADD CHECK(event_sequence>0);
ALTER TABLE companion.outbox ADD UNIQUE(conversation_id,event_sequence);
CREATE FUNCTION companion.assign_event_sequence() RETURNS trigger
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
BEGIN
    UPDATE companion.conversations SET next_event_sequence=next_event_sequence+1
        WHERE id=NEW.conversation_id AND user_id=NEW.user_id
        RETURNING next_event_sequence-1 INTO NEW.event_sequence;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='conversation_unavailable'; END IF;
    RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION companion.assign_event_sequence() FROM PUBLIC;
CREATE TRIGGER assign_event_sequence BEFORE INSERT ON companion.outbox
    FOR EACH ROW EXECUTE FUNCTION companion.assign_event_sequence();
INSERT INTO companion.schema_migrations(version) VALUES(7);
COMMIT;
