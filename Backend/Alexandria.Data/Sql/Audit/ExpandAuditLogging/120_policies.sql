-- Rule parameters are free-form JSON, so only the allowlisted columns are copied; parameter
-- edits show up as a flag.
CREATE OR REPLACE FUNCTION audit_directory_policies_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_directory_id UUID := COALESCE(NEW."DirectoryId", OLD."DirectoryId");
    v_owner_id     UUID;
    v_dir_name     TEXT;
BEGIN
    BEGIN
        SELECT "OwnerId", "Name" INTO v_owner_id, v_dir_name FROM "Directories" WHERE "Id" = v_directory_id;

        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('Policy', NEW."Id", 'PolicyCreated', 'Create',
                jsonb_build_object(
                    'directory_id',          v_directory_id,
                    'inherited_by_children', NEW."InheritedByChildren"),
                v_owner_id, v_directory_id, v_dir_name);

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."InheritedByChildren" IS DISTINCT FROM NEW."InheritedByChildren" THEN
                PERFORM audit_write('Policy', NEW."Id", 'PolicyUpdated', 'Update',
                    jsonb_build_object(
                        'directory_id', v_directory_id,
                        'changes',      jsonb_build_object('inherited_by_children', jsonb_build_object(
                                            'old', OLD."InheritedByChildren",
                                            'new', NEW."InheritedByChildren"))),
                    v_owner_id, v_directory_id, v_dir_name);
            END IF;

            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('Policy', NEW."Id", 'PolicyDeleted', 'Update',
                    jsonb_build_object('directory_id', v_directory_id),
                    v_owner_id, v_directory_id, v_dir_name);
            END IF;

        ELSIF TG_OP = 'DELETE' AND v_dir_name IS NOT NULL THEN
            PERFORM audit_write('Policy', OLD."Id", 'PolicyDeleted', 'Delete',
                jsonb_build_object('directory_id', v_directory_id),
                v_owner_id, v_directory_id, v_dir_name);
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION audit_policy_rules_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_policy_id    UUID := COALESCE(NEW."PolicyId", OLD."PolicyId");
    v_directory_id UUID;
    v_owner_id     UUID;
    v_dir_name     TEXT;
    v_rule         JSONB;
    v_changes      JSONB;
BEGIN
    BEGIN
        SELECT d."Id", d."OwnerId", d."Name" INTO v_directory_id, v_owner_id, v_dir_name
        FROM "DirectoryPolicies" p
        JOIN "Directories" d ON d."Id" = p."DirectoryId"
        WHERE p."Id" = v_policy_id;

        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('Policy', v_policy_id, 'PolicyRuleCreated', 'Create',
                jsonb_build_object(
                    'rule_id',             NEW."Id",
                    'directory_id',        v_directory_id,
                    'action_type',         NEW."ActionType",
                    'trigger_type',        NEW."TriggerType",
                    'trigger_value',       NEW."TriggerValue",
                    'priority',            NEW."Priority",
                    'apply_on_new_version', NEW."ApplyOnNewVersion"),
                v_owner_id, v_directory_id, v_dir_name);

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('Policy', v_policy_id, 'PolicyRuleDeleted', 'Update',
                    jsonb_build_object(
                        'rule_id',      NEW."Id",
                        'directory_id', v_directory_id,
                        'action_type',  NEW."ActionType"),
                    v_owner_id, v_directory_id, v_dir_name);
            ELSE
                v_rule := audit_jsonb_changes(
                    jsonb_build_object(
                        'action_type',          OLD."ActionType",
                        'trigger_type',         OLD."TriggerType",
                        'trigger_value',        OLD."TriggerValue",
                        'priority',             OLD."Priority",
                        'apply_on_new_version', OLD."ApplyOnNewVersion"),
                    jsonb_build_object(
                        'action_type',          NEW."ActionType",
                        'trigger_type',         NEW."TriggerType",
                        'trigger_value',        NEW."TriggerValue",
                        'priority',             NEW."Priority",
                        'apply_on_new_version', NEW."ApplyOnNewVersion"));

                v_changes := v_rule;
                IF OLD."Parameters" IS DISTINCT FROM NEW."Parameters" THEN
                    v_changes := v_changes || jsonb_build_object('parameters', jsonb_build_object('changed', true));
                END IF;

                IF v_changes <> '{}'::JSONB THEN
                    PERFORM audit_write('Policy', v_policy_id, 'PolicyRuleUpdated', 'Update',
                        jsonb_build_object(
                            'rule_id',      NEW."Id",
                            'directory_id', v_directory_id,
                            'changes',      v_changes),
                        v_owner_id, v_directory_id, v_dir_name);
                END IF;
            END IF;

        ELSIF TG_OP = 'DELETE' AND v_dir_name IS NOT NULL THEN
            PERFORM audit_write('Policy', v_policy_id, 'PolicyRuleDeleted', 'Delete',
                jsonb_build_object(
                    'rule_id',      OLD."Id",
                    'directory_id', v_directory_id,
                    'action_type',  OLD."ActionType"),
                v_owner_id, v_directory_id, v_dir_name);
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS directory_policies_audit_trigger ON "DirectoryPolicies";
DROP TRIGGER IF EXISTS policy_rules_audit_trigger ON "PolicyRules";

CREATE TRIGGER directory_policies_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "InheritedByChildren", "DeletedAt"
    ON "DirectoryPolicies"
    FOR EACH ROW
    EXECUTE FUNCTION audit_directory_policies_changes();

CREATE TRIGGER policy_rules_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF
        "ActionType", "TriggerType", "TriggerValue", "Priority", "Parameters", "ApplyOnNewVersion", "DeletedAt"
    ON "PolicyRules"
    FOR EACH ROW
    EXECUTE FUNCTION audit_policy_rules_changes();
