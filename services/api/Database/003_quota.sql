-- Configurable accounting primitives. No production allowance, rate or price is seeded.
BEGIN;
CREATE TABLE companion.global_budgets (
    id uuid PRIMARY KEY,
    unit_type text NOT NULL CHECK(length(unit_type) BETWEEN 1 AND 80),
    limit_units bigint NOT NULL CHECK(limit_units>=0),
    held_units bigint NOT NULL DEFAULT 0 CHECK(held_units>=0),
    spent_units bigint NOT NULL DEFAULT 0 CHECK(spent_units>=0),
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NOT NULL,
    CHECK(ends_at>starts_at),
    CHECK(spent_units<=limit_units AND held_units<=limit_units-spent_units)
);
CREATE TABLE companion.user_budgets (
    user_id uuid NOT NULL REFERENCES companion.users(id),
    budget_id uuid NOT NULL REFERENCES companion.global_budgets(id),
    limit_units bigint NOT NULL CHECK(limit_units>=0),
    held_units bigint NOT NULL DEFAULT 0 CHECK(held_units>=0),
    spent_units bigint NOT NULL DEFAULT 0 CHECK(spent_units>=0),
    PRIMARY KEY(user_id,budget_id),
    CHECK(spent_units<=limit_units AND held_units<=limit_units-spent_units)
);
CREATE TABLE companion.usage_reservations (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    budget_id uuid NOT NULL,
    request_key uuid NOT NULL,
    reserved_units bigint NOT NULL CHECK(reserved_units>0),
    settled_units bigint CHECK(settled_units>=0 AND settled_units<=reserved_units),
    state text NOT NULL DEFAULT 'reserved' CHECK(state IN ('reserved','settled','released')),
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK((state='reserved' AND settled_units IS NULL) OR (state='settled' AND settled_units>0) OR (state='released' AND settled_units=0)),
    UNIQUE(user_id,request_key),
    UNIQUE(id,user_id),
    FOREIGN KEY(user_id,budget_id) REFERENCES companion.user_budgets(user_id,budget_id)
);
CREATE INDEX reservation_reconciliation ON companion.usage_reservations(expires_at,id) WHERE state='reserved';
CREATE TABLE companion.usage_ledger (
    reservation_id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    units bigint NOT NULL CHECK(units>=0),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY(reservation_id,user_id) REFERENCES companion.usage_reservations(id,user_id)
);
DO $$ DECLARE t text; BEGIN
    FOREACH t IN ARRAY ARRAY['user_budgets','usage_reservations','usage_ledger'] LOOP
        EXECUTE format('ALTER TABLE companion.%I ENABLE ROW LEVEL SECURITY',t);
        EXECUTE format('ALTER TABLE companion.%I FORCE ROW LEVEL SECURITY',t);
        EXECUTE format('CREATE POLICY owner_only ON companion.%I USING(user_id=companion.actor_id()) WITH CHECK(user_id=companion.actor_id())',t);
    END LOOP;
END $$;
GRANT SELECT ON companion.global_budgets,companion.user_budgets TO companion_runtime;
GRANT UPDATE(held_units,spent_units) ON companion.global_budgets,companion.user_budgets TO companion_runtime;
GRANT SELECT,INSERT ON companion.usage_reservations,companion.usage_ledger TO companion_runtime;
GRANT UPDATE(state,settled_units) ON companion.usage_reservations TO companion_runtime;

CREATE FUNCTION companion.reserve_usage(p_budget uuid,p_key uuid,p_units bigint,p_expires timestamptz)
RETURNS uuid LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); g companion.global_budgets%ROWTYPE;
    u companion.user_budgets%ROWTYPE; prior companion.usage_reservations%ROWTYPE; result uuid;
BEGIN
    IF actor IS NULL OR p_key IS NULL OR p_units IS NULL OR p_units<=0 OR p_expires IS NULL THEN
        RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_reservation';
    END IF;
    -- Same owner-first ordering as conversation admission permits caller transactions.
    PERFORM 1 FROM companion.users WHERE id=actor AND status='active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT * INTO prior FROM companion.usage_reservations WHERE user_id=actor AND request_key=p_key;
    IF FOUND THEN
        IF prior.budget_id IS DISTINCT FROM p_budget OR prior.reserved_units<>p_units OR prior.expires_at<>p_expires THEN
            RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='reservation_conflict';
        END IF;
        RETURN prior.id;
    END IF;
    SELECT * INTO g FROM companion.global_budgets WHERE id=p_budget FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='budget_unavailable'; END IF;
    SELECT * INTO u FROM companion.user_budgets WHERE user_id=actor AND budget_id=p_budget FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='budget_unavailable'; END IF;
    IF clock_timestamp()<g.starts_at OR clock_timestamp()>=g.ends_at OR p_expires<=clock_timestamp() OR p_expires>g.ends_at THEN
        RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_budget_window';
    END IF;
    IF p_units>g.limit_units-g.spent_units-g.held_units OR p_units>u.limit_units-u.spent_units-u.held_units THEN
        RAISE EXCEPTION USING ERRCODE='P0001',MESSAGE='quota_exceeded';
    END IF;
    result:=gen_random_uuid();
    UPDATE companion.global_budgets SET held_units=held_units+p_units WHERE id=p_budget;
    UPDATE companion.user_budgets SET held_units=held_units+p_units WHERE user_id=actor AND budget_id=p_budget;
    INSERT INTO companion.usage_reservations(id,user_id,budget_id,request_key,reserved_units,expires_at)
        VALUES(result,actor,p_budget,p_key,p_units,p_expires);
    RETURN result;
END $$;

CREATE FUNCTION companion.settle_usage(p_reservation uuid,p_actual bigint) RETURNS uuid
LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); reservation companion.usage_reservations%ROWTYPE;
BEGIN
    IF actor IS NULL OR p_actual IS NULL OR p_actual<0 THEN RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_settlement'; END IF;
    -- Pending usage must remain reconcilable after account deletion has started.
    PERFORM 1 FROM companion.users WHERE id=actor FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT * INTO reservation FROM companion.usage_reservations WHERE id=p_reservation AND user_id=actor FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='reservation_unavailable'; END IF;
    IF reservation.state<>'reserved' THEN
        IF reservation.settled_units=p_actual THEN RETURN reservation.id; END IF;
        RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='settlement_conflict';
    END IF;
    IF p_actual>reservation.reserved_units THEN RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='usage_exceeds_reservation'; END IF;
    PERFORM 1 FROM companion.global_budgets WHERE id=reservation.budget_id FOR UPDATE;
    PERFORM 1 FROM companion.user_budgets WHERE user_id=actor AND budget_id=reservation.budget_id FOR UPDATE;
    UPDATE companion.global_budgets SET held_units=held_units-reservation.reserved_units,spent_units=spent_units+p_actual WHERE id=reservation.budget_id;
    UPDATE companion.user_budgets SET held_units=held_units-reservation.reserved_units,spent_units=spent_units+p_actual WHERE user_id=actor AND budget_id=reservation.budget_id;
    UPDATE companion.usage_reservations SET settled_units=p_actual,state=CASE WHEN p_actual=0 THEN 'released' ELSE 'settled' END WHERE id=reservation.id AND user_id=actor;
    INSERT INTO companion.usage_ledger(reservation_id,user_id,units) VALUES(reservation.id,actor,p_actual);
    RETURN reservation.id;
END $$;
REVOKE ALL ON FUNCTION companion.reserve_usage(uuid,uuid,bigint,timestamptz),companion.settle_usage(uuid,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.reserve_usage(uuid,uuid,bigint,timestamptz),companion.settle_usage(uuid,bigint) TO companion_runtime;
INSERT INTO companion.schema_migrations(version) VALUES(3);
COMMIT;
