-- Shared helpers for every audit trigger. Per-table functions only decide the event code and
-- metadata; actor, IP, correlation id and the insert itself live here.

CREATE OR REPLACE FUNCTION audit_setting_uuid(p_name TEXT)
RETURNS UUID
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    RETURN NULLIF(current_setting(p_name, true), '')::UUID;
EXCEPTION WHEN invalid_text_representation THEN
    RETURN NULL;
END;
$$;

-- Workers, background services and anonymous requests have no user behind them.
CREATE OR REPLACE FUNCTION audit_actor_is_system()
RETURNS BOOLEAN
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(audit_setting_uuid('app.current_user_id'), '00000000-0000-0000-0000-000000000001'::UUID)
           = '00000000-0000-0000-0000-000000000001'::UUID;
$$;

CREATE OR REPLACE FUNCTION audit_operation(p_tg_op TEXT)
RETURNS TEXT
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE p_tg_op
        WHEN 'INSERT' THEN 'Create'
        WHEN 'UPDATE' THEN 'Update'
        ELSE 'Delete'
    END;
$$;

CREATE OR REPLACE FUNCTION audit_directory_name(p_directory_id UUID)
RETURNS TEXT
LANGUAGE sql
STABLE
AS $$
    SELECT "Name" FROM "Directories" WHERE "Id" = p_directory_id;
$$;

CREATE OR REPLACE FUNCTION audit_tag_name(p_tag_id UUID)
RETURNS TEXT
LANGUAGE sql
STABLE
AS $$
    SELECT "Name" FROM "Tags" WHERE "Id" = p_tag_id;
$$;

-- now() is the transaction start, so a match means the row was written by this transaction.
CREATE OR REPLACE FUNCTION audit_logged_in_transaction(p_entity_type TEXT, p_entity_id UUID, p_event_code TEXT)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1 FROM "AuditLogs"
        WHERE "EntityType" = p_entity_type
          AND "EntityId" = p_entity_id
          AND "EventCode" = p_event_code
          AND "Timestamp" >= now());
$$;

-- Top-level keys whose values differ between two jsonb objects, as {key: {old, new}}.
-- Keys that look like credentials keep only the fact that they changed.
CREATE OR REPLACE FUNCTION audit_jsonb_changes(p_old JSONB, p_new JSONB)
RETURNS JSONB
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT COALESCE(jsonb_object_agg(
               k,
               CASE
                   WHEN k ~* '(secret|password|token|api_?key|credential)'
                       THEN jsonb_build_object('redacted', true)
                   ELSE jsonb_build_object('old', p_old -> k, 'new', p_new -> k)
               END), '{}'::JSONB)
    FROM (
        SELECT jsonb_object_keys(CASE WHEN jsonb_typeof(p_old) = 'object' THEN p_old ELSE '{}'::JSONB END) AS k
        UNION
        SELECT jsonb_object_keys(CASE WHEN jsonb_typeof(p_new) = 'object' THEN p_new ELSE '{}'::JSONB END)
    ) keys
    WHERE (p_old -> k) IS DISTINCT FROM (p_new -> k);
$$;

CREATE OR REPLACE FUNCTION audit_write(
    p_entity_type      TEXT,
    p_entity_id        UUID,
    p_event_code       TEXT,
    p_operation        TEXT,
    p_metadata         JSONB,
    p_owner_id         UUID,
    p_parent_entity_id UUID,
    p_entity_name      TEXT)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
    v_system_id CONSTANT UUID := '00000000-0000-0000-0000-000000000001';
    v_actor     UUID := COALESCE(audit_setting_uuid('app.current_user_id'), v_system_id);
BEGIN
    INSERT INTO "AuditLogs" (
        "Id", "OperationType", "UserId", "EntityType", "EntityId", "EventCode", "MetadataJson",
        "FallbackDescription", "Timestamp", "IpAddress", "Source", "OwnerId", "ParentEntityId",
        "CorrelationId", "EntityName"
    ) VALUES (
        gen_random_uuid(),
        p_operation,
        v_actor,
        p_entity_type,
        p_entity_id,
        p_event_code,
        p_metadata,
        NULL,
        clock_timestamp(),
        left(NULLIF(current_setting('app.current_ip', true), ''), 45),
        CASE WHEN v_actor = v_system_id THEN 'System' ELSE 'Trigger' END,
        p_owner_id,
        p_parent_entity_id,
        audit_setting_uuid('app.correlation_id'),
        left(p_entity_name, 1000)
    );
END;
$$;
