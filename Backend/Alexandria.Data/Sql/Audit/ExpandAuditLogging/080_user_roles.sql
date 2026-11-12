CREATE OR REPLACE FUNCTION audit_user_roles_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_user_id   UUID := COALESCE(NEW."UserId", OLD."UserId");
    v_role_name TEXT;
    v_user_name TEXT;
BEGIN
    BEGIN
        SELECT "UserName" INTO v_user_name FROM "AspNetUsers" WHERE "Id" = v_user_id;

        -- A missing user means the row goes away with the account, which has its own row.
        IF v_user_name IS NOT NULL THEN
            SELECT "Name" INTO v_role_name FROM "AspNetRoles"
            WHERE "Id" = COALESCE(NEW."RoleId", OLD."RoleId");

            IF TG_OP = 'INSERT' THEN
                PERFORM audit_write('User', v_user_id, 'UserRoleGranted', 'Update',
                    jsonb_build_object('role', v_role_name),
                    v_user_id, NULL, v_user_name);
            ELSIF TG_OP = 'DELETE' THEN
                PERFORM audit_write('User', v_user_id, 'UserRoleRevoked', 'Update',
                    jsonb_build_object('role', v_role_name),
                    v_user_id, NULL, v_user_name);
            END IF;
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS user_roles_audit_trigger ON "AspNetUserRoles";

CREATE TRIGGER user_roles_audit_trigger
    AFTER INSERT OR DELETE
    ON "AspNetUserRoles"
    FOR EACH ROW
    EXECUTE FUNCTION audit_user_roles_changes();
