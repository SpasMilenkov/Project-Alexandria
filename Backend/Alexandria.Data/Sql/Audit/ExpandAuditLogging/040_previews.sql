-- Generated previews are derived data and regenerate often, so only user actions are audited:
-- custom track covers, and previews a user removes. Rows deleted because their version is gone
-- are cascades of a version or file deletion that already has its own row.
CREATE OR REPLACE FUNCTION audit_previews_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_version_id     UUID := COALESCE(NEW."VersionId", OLD."VersionId");
    v_file_id        UUID;
    v_owner_id       UUID;
    v_file_name      TEXT;
    v_version_number INT;
    v_metadata       JSONB;
BEGIN
    BEGIN
        SELECT f."Id", f."OwnerId", f."Name", v."VersionNumber"
        INTO v_file_id, v_owner_id, v_file_name, v_version_number
        FROM "FileVersions" v
        JOIN "Files" f ON f."Id" = v."FileId"
        WHERE v."Id" = v_version_id;

        v_metadata := jsonb_build_object(
            'kind',           COALESCE(NEW."Kind", OLD."Kind"),
            'version_id',     v_version_id,
            'version_number', v_version_number,
            'file_id',        v_file_id);

        IF TG_OP = 'INSERT' THEN
            IF NEW."IsManual" THEN
                PERFORM audit_write('Preview', NEW."Id", 'TrackCoverSet', 'Create',
                    v_metadata || jsonb_build_object('mime_type', NEW."MimeType", 'size', NEW."Size"),
                    v_owner_id, v_file_id, v_file_name);
            ELSIF NOT audit_actor_is_system() THEN
                PERFORM audit_write('Preview', NEW."Id", 'PreviewCreated', 'Create',
                    v_metadata || jsonb_build_object('mime_type', NEW."MimeType", 'size', NEW."Size"),
                    v_owner_id, v_file_id, v_file_name);
            END IF;

        ELSIF TG_OP = 'UPDATE' THEN
            IF NEW."IsManual" AND (NOT OLD."IsManual" OR OLD."ObjectKey" IS DISTINCT FROM NEW."ObjectKey") THEN
                PERFORM audit_write('Preview', NEW."Id", 'TrackCoverSet', 'Update',
                    v_metadata || jsonb_build_object(
                        'mime_type', NEW."MimeType",
                        'size',      NEW."Size",
                        'replaced',  OLD."IsManual"),
                    v_owner_id, v_file_id, v_file_name);
            END IF;

            IF OLD."IsManual" AND NOT NEW."IsManual" THEN
                PERFORM audit_write('Preview', NEW."Id", 'TrackCoverRemoved', 'Update',
                    v_metadata, v_owner_id, v_file_id, v_file_name);
            END IF;

            IF NOT audit_actor_is_system() THEN
                IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                    PERFORM audit_write('Preview', NEW."Id", 'PreviewSoftDeleted', 'Delete',
                        v_metadata || jsonb_build_object('deleted_at', NEW."DeletedAt"),
                        v_owner_id, v_file_id, v_file_name);
                END IF;

                IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                    PERFORM audit_write('Preview', NEW."Id", 'PreviewRestored', 'Update',
                        v_metadata, v_owner_id, v_file_id, v_file_name);
                END IF;
            END IF;

        ELSIF TG_OP = 'DELETE' AND v_file_id IS NOT NULL THEN
            IF OLD."IsManual" THEN
                PERFORM audit_write('Preview', OLD."Id", 'TrackCoverRemoved', 'Delete',
                    v_metadata, v_owner_id, v_file_id, v_file_name);
            ELSIF NOT audit_actor_is_system() THEN
                PERFORM audit_write('Preview', OLD."Id", 'PreviewDeletedPermanently', 'Delete',
                    v_metadata || jsonb_build_object('mime_type', OLD."MimeType", 'size', OLD."Size"),
                    v_owner_id, v_file_id, v_file_name);
            END IF;
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS previews_audit_trigger ON "Previews";

CREATE TRIGGER previews_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "IsManual", "ObjectKey", "DeletedAt"
    ON "Previews"
    FOR EACH ROW
    EXECUTE FUNCTION audit_previews_changes();
