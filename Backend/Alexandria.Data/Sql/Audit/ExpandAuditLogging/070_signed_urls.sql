-- The token is a bearer credential and never reaches the audit log.
CREATE OR REPLACE FUNCTION audit_signed_urls_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_file_name TEXT;
BEGIN
    BEGIN
        SELECT "Name" INTO v_file_name FROM "Files" WHERE "Id" = COALESCE(NEW."FileId", OLD."FileId");

        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('ShareLink', NEW."Id", 'ShareLinkCreated', 'Create',
                jsonb_build_object(
                    'file_id',          NEW."FileId",
                    'expires_at',       NEW."ExpiresAt",
                    'max_access_count', NEW."MaxAccessCount",
                    'version_id',       NEW."FileVersionId",
                    'version_number',   (SELECT "VersionNumber" FROM "FileVersions" WHERE "Id" = NEW."FileVersionId")),
                NEW."CreatorId", NEW."FileId", v_file_name);

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('ShareLink', NEW."Id", 'ShareLinkRevoked', 'Update',
                    jsonb_build_object(
                        'file_id',      NEW."FileId",
                        'access_count', NEW."AccessCount"),
                    NEW."CreatorId", NEW."FileId", v_file_name);
            END IF;

            IF NEW."AccessCount" > OLD."AccessCount" THEN
                PERFORM audit_write('ShareLink', NEW."Id", 'ShareLinkAccessed', 'Read',
                    jsonb_build_object(
                        'file_id',          NEW."FileId",
                        'access_count',     NEW."AccessCount",
                        'max_access_count', NEW."MaxAccessCount",
                        'exhausted',        NEW."MaxAccessCount" IS NOT NULL
                                            AND NEW."AccessCount" >= NEW."MaxAccessCount"),
                    NEW."CreatorId", NEW."FileId", v_file_name);
            END IF;

            IF OLD."FileVersionId" IS DISTINCT FROM NEW."FileVersionId" THEN
                PERFORM audit_write('ShareLink', NEW."Id", 'ShareLinkVersionChanged', 'Update',
                    jsonb_build_object(
                        'file_id',        NEW."FileId",
                        'old',            (SELECT "VersionNumber" FROM "FileVersions" WHERE "Id" = OLD."FileVersionId"),
                        'new',            (SELECT "VersionNumber" FROM "FileVersions" WHERE "Id" = NEW."FileVersionId"),
                        'old_version_id', OLD."FileVersionId",
                        'new_version_id', NEW."FileVersionId"),
                    NEW."CreatorId", NEW."FileId", v_file_name);
            END IF;
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS signed_urls_audit_trigger ON "SignedUrls";

-- Hard deletes only happen when the file itself is deleted, which has its own row.
CREATE TRIGGER signed_urls_audit_trigger
    AFTER INSERT OR UPDATE OF "DeletedAt", "AccessCount", "FileVersionId"
    ON "SignedUrls"
    FOR EACH ROW
    EXECUTE FUNCTION audit_signed_urls_changes();
