-- Auto-tag sweeps and embedded tags read during processing are derived data, so only tags a
-- user applies or removes are audited. Rows that vanish with their file or tag are cascades.
CREATE OR REPLACE FUNCTION audit_file_tags_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_file_id   UUID := COALESCE(NEW."FileId", OLD."FileId");
    v_tag_id    UUID := COALESCE(NEW."TagId", OLD."TagId");
    v_owner_id  UUID;
    v_file_name TEXT;
    v_tag_name  TEXT;
BEGIN
    BEGIN
        IF NOT audit_actor_is_system() THEN
            SELECT "OwnerId", "Name" INTO v_owner_id, v_file_name FROM "Files" WHERE "Id" = v_file_id;
            v_tag_name := audit_tag_name(v_tag_id);

            IF v_file_name IS NOT NULL AND v_tag_name IS NOT NULL THEN
                IF TG_OP = 'INSERT' THEN
                    PERFORM audit_write('FileTag', v_tag_id, 'FileTagApplied', 'Create',
                        jsonb_build_object(
                            'tag_id',     v_tag_id,
                            'file_id',    v_file_id,
                            'file_name',  v_file_name,
                            'source',     NEW."Source",
                            'confidence', NEW."Confidence"),
                        v_owner_id, v_file_id, v_tag_name);
                ELSIF TG_OP = 'DELETE' THEN
                    PERFORM audit_write('FileTag', v_tag_id, 'FileTagRemoved', 'Delete',
                        jsonb_build_object(
                            'tag_id',    v_tag_id,
                            'file_id',   v_file_id,
                            'file_name', v_file_name,
                            'source',    OLD."Source"),
                        v_owner_id, v_file_id, v_tag_name);
                END IF;
            END IF;
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS file_tags_audit_trigger ON "FileTags";

CREATE TRIGGER file_tags_audit_trigger
    AFTER INSERT OR DELETE
    ON "FileTags"
    FOR EACH ROW
    EXECUTE FUNCTION audit_file_tags_changes();
