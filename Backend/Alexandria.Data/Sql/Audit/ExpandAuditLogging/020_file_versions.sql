CREATE OR REPLACE FUNCTION audit_file_versions_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_file_id   UUID := COALESCE(NEW."FileId", OLD."FileId");
    v_owner_id  UUID;
    v_file_name TEXT;
BEGIN
    BEGIN
        SELECT "OwnerId", "Name" INTO v_owner_id, v_file_name
        FROM "Files" WHERE "Id" = v_file_id;

        -- When a file is hard-deleted its versions go by cascade after the file row is already
        -- gone, so the uploader stands in as owner rather than leaving the row ownerless.
        v_owner_id := COALESCE(v_owner_id, NEW."CreatedBy", OLD."CreatedBy");

        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('FileVersion', NEW."Id", 'FileVersionCreated', 'Create',
                jsonb_build_object(
                    'file_id',        NEW."FileId",
                    'version_number', NEW."VersionNumber",
                    'mime_type',      NEW."MimeType",
                    'size',           NEW."Size"),
                v_owner_id, v_file_id, v_file_name);

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('FileVersion', NEW."Id", 'FileVersionSoftDeleted', 'Delete',
                    jsonb_build_object(
                        'file_id',        NEW."FileId",
                        'version_number', NEW."VersionNumber",
                        'deleted_at',     NEW."DeletedAt"),
                    v_owner_id, v_file_id, v_file_name);
            END IF;

            IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                PERFORM audit_write('FileVersion', NEW."Id", 'FileVersionRestored', 'Update',
                    jsonb_build_object(
                        'file_id',        NEW."FileId",
                        'version_number', NEW."VersionNumber"),
                    v_owner_id, v_file_id, v_file_name);
            END IF;

        ELSIF TG_OP = 'DELETE' THEN
            PERFORM audit_write('FileVersion', OLD."Id", 'FileVersionDeletedPermanently', 'Delete',
                jsonb_build_object(
                    'file_id',        OLD."FileId",
                    'version_number', OLD."VersionNumber",
                    'mime_type',      OLD."MimeType",
                    'size',           OLD."Size"),
                v_owner_id, v_file_id, v_file_name);
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS file_versions_audit_trigger ON "FileVersions";

CREATE TRIGGER file_versions_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "DeletedAt"
    ON "FileVersions"
    FOR EACH ROW
    EXECUTE FUNCTION audit_file_versions_changes();
