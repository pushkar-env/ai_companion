-- Local M2 foundation. Apply once as a migration owner, never as the runtime role.
-- The deployment/bootstrap layer supplies the non-owner companion_runtime role.
BEGIN;
CREATE SCHEMA companion;
REVOKE ALL ON SCHEMA companion FROM PUBLIC;
CREATE TABLE companion.schema_migrations (
    version integer PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE companion.users (
    id uuid PRIMARY KEY,
    locale text NOT NULL CHECK (locale IN ('en-IN','hi-IN')),
    status text NOT NULL DEFAULT 'active' CHECK (status IN ('active','deleting','deleted')),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE companion.companions (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES companion.users(id),
    name text NOT NULL CHECK (length(name) BETWEEN 1 AND 80),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(id,user_id)
);
CREATE TABLE companion.conversations (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES companion.users(id),
    companion_id uuid NOT NULL,
    state text NOT NULL DEFAULT 'open' CHECK (state IN ('open','closed')),
    next_sequence bigint NOT NULL DEFAULT 1 CHECK (next_sequence > 0),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    latest_turn_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(id,user_id),
    FOREIGN KEY(companion_id,user_id) REFERENCES companion.companions(id,user_id)
);
CREATE INDEX conversations_owner_recent ON companion.conversations(user_id,latest_turn_at DESC,id);
CREATE TABLE companion.turns (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    state text NOT NULL DEFAULT 'accepted' CHECK (state IN ('accepted','generating','completed','cancelled','failed')),
    modality text NOT NULL DEFAULT 'text' CHECK (modality = 'text'),
    config_version text NOT NULL CHECK (length(config_version) BETWEEN 1 AND 80),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(id,user_id,conversation_id),
    FOREIGN KEY(conversation_id,user_id) REFERENCES companion.conversations(id,user_id)
);
CREATE UNIQUE INDEX one_active_turn ON companion.turns(conversation_id)
    WHERE state IN ('accepted','generating');
CREATE TABLE companion.messages (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    turn_id uuid NOT NULL,
    role text NOT NULL CHECK (role IN ('user','assistant')),
    text text NOT NULL CHECK (length(text) BETWEEN 1 AND 8000),
    sequence bigint NOT NULL CHECK (sequence > 0),
    client_message_id uuid,
    status text NOT NULL CHECK (status IN ('accepted','completed','cancelled','failed')),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(conversation_id,sequence),
    UNIQUE(user_id,client_message_id),
    FOREIGN KEY(turn_id,user_id,conversation_id) REFERENCES companion.turns(id,user_id,conversation_id)
);
CREATE TABLE companion.idempotency_requests (
    user_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    key uuid NOT NULL,
    request_hash bytea NOT NULL CHECK (octet_length(request_hash)=32),
    turn_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id,conversation_id,key),
    FOREIGN KEY(turn_id,user_id,conversation_id) REFERENCES companion.turns(id,user_id,conversation_id)
);
CREATE TABLE companion.outbox (
    event_id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    turn_id uuid NOT NULL,
    aggregate_version bigint NOT NULL CHECK (aggregate_version > 0),
    event_type text NOT NULL CHECK (event_type = 'turn.accepted'),
    created_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz,
    UNIQUE(turn_id,event_type,aggregate_version),
    FOREIGN KEY(turn_id,user_id,conversation_id) REFERENCES companion.turns(id,user_id,conversation_id)
);
CREATE INDEX outbox_pending ON companion.outbox(created_at,event_id) WHERE published_at IS NULL;

CREATE FUNCTION companion.actor_id() RETURNS uuid LANGUAGE sql STABLE
    SET search_path = pg_catalog
    AS $$ SELECT nullif(current_setting('companion.user_id', true),'')::uuid $$;
REVOKE ALL ON FUNCTION companion.actor_id() FROM PUBLIC;
GRANT USAGE ON SCHEMA companion TO companion_runtime;
GRANT EXECUTE ON FUNCTION companion.actor_id() TO companion_runtime;

ALTER TABLE companion.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE companion.users FORCE ROW LEVEL SECURITY;
CREATE POLICY owner_only ON companion.users USING (id=companion.actor_id()) WITH CHECK (id=companion.actor_id());
DO $$ DECLARE t text; BEGIN
    FOREACH t IN ARRAY ARRAY['companions','conversations','turns','messages','idempotency_requests','outbox'] LOOP
        EXECUTE format('ALTER TABLE companion.%I ENABLE ROW LEVEL SECURITY',t);
        EXECUTE format('ALTER TABLE companion.%I FORCE ROW LEVEL SECURITY',t);
        EXECUTE format('CREATE POLICY owner_only ON companion.%I USING (user_id=companion.actor_id()) WITH CHECK (user_id=companion.actor_id())',t);
    END LOOP;
END $$;
GRANT SELECT ON companion.users,companion.companions TO companion_runtime;
GRANT SELECT,INSERT,UPDATE ON companion.conversations,companion.turns TO companion_runtime;
GRANT SELECT,INSERT ON companion.messages,companion.idempotency_requests,companion.outbox TO companion_runtime;

-- Invoker retains RLS; authenticated application code must set actor per transaction.
-- This slice has no public HTTP admission, quota or production identity implementation.
CREATE FUNCTION companion.accept_text(p_conversation uuid,p_key uuid,p_client uuid,p_text text)
RETURNS uuid LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog,companion AS $$
DECLARE actor uuid:=companion.actor_id(); fingerprint bytea; previous companion.messages%ROWTYPE;
    replay companion.idempotency_requests%ROWTYPE; accepted uuid; sequence_value bigint;
BEGIN
    IF actor IS NULL OR p_key IS NULL OR p_client IS NULL OR p_text IS NULL
       OR length(btrim(p_text))=0 OR length(p_text)>8000 THEN
        RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='invalid_admission';
    END IF;
    -- Lock the owner first, serializing client-message dedupe across conversations.
    PERFORM 1 FROM companion.users WHERE id=actor AND status='active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='owner_unavailable'; END IF;
    SELECT next_sequence INTO sequence_value FROM companion.conversations
        WHERE id=p_conversation AND user_id=actor AND state='open' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='conversation_unavailable'; END IF;
    fingerprint:=sha256(convert_to(p_client::text||':'||p_text,'UTF8'));
    SELECT * INTO replay FROM companion.idempotency_requests
        WHERE user_id=actor AND conversation_id=p_conversation AND key=p_key;
    IF FOUND THEN
        IF replay.request_hash<>fingerprint THEN RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='idempotency_conflict'; END IF;
        RETURN replay.turn_id;
    END IF;
    SELECT * INTO previous FROM companion.messages WHERE user_id=actor AND client_message_id=p_client;
    IF FOUND THEN
        IF previous.conversation_id<>p_conversation OR previous.text<>p_text THEN
            RAISE EXCEPTION USING ERRCODE='23505',MESSAGE='client_message_conflict';
        END IF;
        accepted:=previous.turn_id;
    ELSE
        accepted:=gen_random_uuid();
        INSERT INTO companion.turns(id,user_id,conversation_id,config_version)
            VALUES(accepted,actor,p_conversation,'local-synthetic-v1');
        INSERT INTO companion.messages(id,user_id,conversation_id,turn_id,role,text,sequence,client_message_id,status)
            VALUES(gen_random_uuid(),actor,p_conversation,accepted,'user',p_text,sequence_value,p_client,'accepted');
        UPDATE companion.conversations SET next_sequence=next_sequence+1,version=version+1,latest_turn_at=now()
            WHERE id=p_conversation AND user_id=actor;
        INSERT INTO companion.outbox(event_id,user_id,conversation_id,turn_id,aggregate_version,event_type)
            VALUES(gen_random_uuid(),actor,p_conversation,accepted,1,'turn.accepted');
    END IF;
    INSERT INTO companion.idempotency_requests(user_id,conversation_id,key,request_hash,turn_id)
        VALUES(actor,p_conversation,p_key,fingerprint,accepted);
    RETURN accepted;
END $$;
REVOKE ALL ON FUNCTION companion.accept_text(uuid,uuid,uuid,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION companion.accept_text(uuid,uuid,uuid,text) TO companion_runtime;
-- SELECT FOR UPDATE also requires UPDATE privilege on at least one column.
GRANT UPDATE(version) ON companion.users TO companion_runtime;
INSERT INTO companion.schema_migrations(version) VALUES(1);
COMMIT;
