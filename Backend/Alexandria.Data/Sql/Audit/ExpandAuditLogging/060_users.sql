CREATE OR REPLACE FUNCTION audit_users_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_was_locked BOOLEAN;
    v_is_locked  BOOLEAN;
BEGIN
    BEGIN
        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('User', NEW."Id", 'UserCreated', 'Create',
                jsonb_build_object(
                    'username',        NEW."UserName",
                    'email',           NEW."Email",
                    'email_confirmed', NEW."EmailConfirmed",
                    'storage_quota',   NEW."StorageQuota"),
                NEW."Id", NULL, NEW."UserName");

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."Email" IS DISTINCT FROM NEW."Email" THEN
                PERFORM audit_write('User', NEW."Id", 'UserEmailChanged', 'Update',
                    jsonb_build_object('old', OLD."Email", 'new', NEW."Email"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."PasswordHash" IS DISTINCT FROM NEW."PasswordHash" THEN
                PERFORM audit_write('User', NEW."Id", 'UserPasswordChanged', 'Update',
                    jsonb_build_object('changed_at', clock_timestamp()),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."UserName" IS DISTINCT FROM NEW."UserName" THEN
                PERFORM audit_write('User', NEW."Id", 'UserRenamed', 'Update',
                    jsonb_build_object('field', 'username', 'old', OLD."UserName", 'new', NEW."UserName"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."Name" IS DISTINCT FROM NEW."Name" THEN
                PERFORM audit_write('User', NEW."Id", 'UserRenamed', 'Update',
                    jsonb_build_object('field', 'name', 'old', OLD."Name", 'new', NEW."Name"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."StorageQuota" IS DISTINCT FROM NEW."StorageQuota" THEN
                PERFORM audit_write('User', NEW."Id", 'UserQuotaChanged', 'Update',
                    jsonb_build_object('old', OLD."StorageQuota", 'new', NEW."StorageQuota"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            -- LockoutEnabled only says whether lockout may happen at all; the account is locked
            -- while LockoutEnd lies in the future.
            v_was_locked := OLD."LockoutEnd" IS NOT NULL AND OLD."LockoutEnd" > now();
            v_is_locked  := NEW."LockoutEnd" IS NOT NULL AND NEW."LockoutEnd" > now();

            IF v_is_locked AND OLD."LockoutEnd" IS DISTINCT FROM NEW."LockoutEnd" THEN
                PERFORM audit_write('User', NEW."Id", 'UserLockedOut', 'Update',
                    jsonb_build_object(
                        'end',        NEW."LockoutEnd",
                        'started_at', NEW."LockoutStartedAt",
                        'extended',   v_was_locked),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF v_was_locked AND NOT v_is_locked THEN
                PERFORM audit_write('User', NEW."Id", 'UserLockoutLifted', 'Update',
                    jsonb_build_object('previous_end', OLD."LockoutEnd"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."TwoFactorEnabled" IS DISTINCT FROM NEW."TwoFactorEnabled" THEN
                PERFORM audit_write('User', NEW."Id",
                    CASE WHEN NEW."TwoFactorEnabled" THEN 'UserTwoFactorEnabled' ELSE 'UserTwoFactorDisabled' END,
                    'Update',
                    jsonb_build_object('enabled', NEW."TwoFactorEnabled"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('User', NEW."Id", 'UserSoftDeleted', 'Delete',
                    jsonb_build_object('username', NEW."UserName", 'deleted_at', NEW."DeletedAt"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

            IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                PERFORM audit_write('User', NEW."Id", 'UserRestored', 'Update',
                    jsonb_build_object('username', NEW."UserName"),
                    NEW."Id", NULL, NEW."UserName");
            END IF;

        ELSIF TG_OP = 'DELETE' THEN
            PERFORM audit_write('User', OLD."Id", 'UserDeleted', 'Delete',
                jsonb_build_object('username', OLD."UserName", 'email', OLD."Email"),
                OLD."Id", NULL, OLD."UserName");
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS users_audit_trigger ON "AspNetUsers";

CREATE TRIGGER users_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF
        "Email", "PasswordHash", "UserName", "Name", "StorageQuota", "LockoutEnd",
        "TwoFactorEnabled", "DeletedAt"
    ON "AspNetUsers"
    FOR EACH ROW
    EXECUTE FUNCTION audit_users_changes();
