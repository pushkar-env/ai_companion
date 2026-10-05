BEGIN;
ALTER TABLE companion.turns ADD COLUMN reservation_id uuid;
ALTER TABLE companion.turns ADD CONSTRAINT turn_reservation_owner
    FOREIGN KEY(reservation_id,user_id) REFERENCES companion.usage_reservations(id,user_id);
CREATE UNIQUE INDEX reservation_one_turn ON companion.turns(reservation_id) WHERE reservation_id IS NOT NULL;

CREATE FUNCTION companion.admit_metered_text(p_conversation uuid,p_key uuid,p_client uuid,p_text text,p_budget uuid,p_units bigint)
RETURNS uuid LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE accepted uuid; reserved uuid; linked uuid; deadline timestamptz;
BEGIN
    -- Acceptance, retry keys, outbox, hold and binding share the caller transaction.
    -- Accept first so a same-client-message retry under a new key reuses its hold.
    accepted:=companion.accept_text(p_conversation,p_key,p_client,p_text);
    SELECT reservation_id INTO linked FROM companion.turns WHERE id=accepted AND user_id=companion.actor_id();
    IF linked IS NOT NULL THEN
        IF NOT EXISTS(SELECT 1 FROM companion.usage_reservations WHERE id=linked AND user_id=companion.actor_id()
            AND budget_id=p_budget AND reserved_units=p_units) THEN
            RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='admission_config_conflict';
        END IF;
        RETURN accepted;
    END IF;
    IF EXISTS(SELECT 1 FROM companion.turns WHERE id=accepted AND state<>'accepted') THEN
        RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='unmetered_terminal_turn';
    END IF;
    SELECT ends_at INTO deadline FROM companion.global_budgets WHERE id=p_budget;
    reserved:=companion.reserve_usage(p_budget,accepted,p_units,deadline);
    UPDATE companion.turns SET reservation_id=reserved WHERE id=accepted AND user_id=companion.actor_id();
    RETURN accepted;
END $$;
REVOKE ALL ON FUNCTION companion.admit_metered_text(uuid,uuid,uuid,text,uuid,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.admit_metered_text(uuid,uuid,uuid,text,uuid,bigint) TO companion_runtime;
INSERT INTO companion.schema_migrations(version) VALUES(4);
COMMIT;
