-- Settings values are JSON objects, so rows record the changed top-level keys rather than whole
-- documents. audit_jsonb_changes redacts keys that look like credentials.
CREATE OR REPLACE FUNCTION audit_settings_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_is_admin  BOOLEAN := TG_TABLE_NAME = 'AdminSettings';
    v_code      TEXT;
    v_owner_id  UUID;
    v_changes   JSONB;
BEGIN
    BEGIN
        v_code := CASE WHEN v_is_admin THEN 'AdminSettingChanged' ELSE 'SettingChanged' END;

        IF NOT v_is_admin THEN
            v_owner_id := COALESCE(NEW."UserId", OLD."UserId");
        END IF;

        IF TG_OP = 'INSERT' THEN
            v_changes := audit_jsonb_changes('{}'::JSONB, NEW."Value");
        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                v_changes := audit_jsonb_changes(OLD."Value", '{}'::JSONB);
            ELSE
                v_changes := audit_jsonb_changes(OLD."Value", NEW."Value");
            END IF;
        ELSE
            v_changes := audit_jsonb_changes(OLD."Value", '{}'::JSONB);
        END IF;

        -- Scalar values have no keys to compare, so the change is recorded as a whole.
        IF v_changes = '{}'::JSONB AND TG_OP <> 'DELETE'
           AND jsonb_typeof(COALESCE(NEW."Value", 'null'::JSONB)) <> 'object'
           AND OLD."Value" IS DISTINCT FROM NEW."Value" THEN
            v_changes := jsonb_build_object('value', jsonb_build_object('old', OLD."Value", 'new', NEW."Value"));
        END IF;

        IF v_changes <> '{}'::JSONB THEN
            PERFORM audit_write('Setting', COALESCE(NEW."Id", OLD."Id"), v_code, audit_operation(TG_OP),
                jsonb_build_object(
                    'key',     COALESCE(NEW."Key", OLD."Key"),
                    'changes', v_changes,
                    'reset',   TG_OP = 'DELETE' OR (NEW."DeletedAt" IS NOT NULL)),
                v_owner_id, NULL, COALESCE(NEW."Key", OLD."Key"));
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS admin_settings_audit_trigger ON "AdminSettings";
DROP TRIGGER IF EXISTS settings_audit_trigger ON "Settings";

CREATE TRIGGER admin_settings_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "Value", "DeletedAt"
    ON "AdminSettings"
    FOR EACH ROW
    EXECUTE FUNCTION audit_settings_changes();

CREATE TRIGGER settings_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "Value", "DeletedAt"
    ON "Settings"
    FOR EACH ROW
    EXECUTE FUNCTION audit_settings_changes();
