CREATE OR REPLACE FUNCTION audit_files_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    BEGIN
        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('File', NEW."Id", 'FileCreated', 'Create',
                jsonb_build_object(
                    'name',         NEW."Name",
                    'mime_type',    NEW."MimeType",
                    'directory_id', NEW."DirectoryId"),
                NEW."OwnerId", NULL, NEW."Name");

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."Name" IS DISTINCT FROM NEW."Name" THEN
                PERFORM audit_write('File', NEW."Id", 'FileRenamed', 'Update',
                    jsonb_build_object('old', OLD."Name", 'new', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DirectoryId" IS DISTINCT FROM NEW."DirectoryId" THEN
                PERFORM audit_write('File', NEW."Id", 'FileMoved', 'Update',
                    jsonb_build_object(
                        'old',              audit_directory_name(OLD."DirectoryId"),
                        'new',              audit_directory_name(NEW."DirectoryId"),
                        'old_directory_id', OLD."DirectoryId",
                        'new_directory_id', NEW."DirectoryId",
                        'name',             NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('File', NEW."Id", 'FileSoftDeleted', 'Delete',
                    jsonb_build_object('name', NEW."Name", 'deleted_at', NEW."DeletedAt"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                PERFORM audit_write('File', NEW."Id", 'FileRestored', 'Update',
                    jsonb_build_object('name', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            -- The first version is linked right after the file insert, and every upload of a new
            -- version moves the pointer too. Only a switch to a version that already existed is
            -- an event of its own; uploads already have their FileVersionCreated row.
            IF OLD."CurrentVersionId" IS NOT NULL
               AND NEW."CurrentVersionId" IS NOT NULL
               AND OLD."CurrentVersionId" <> NEW."CurrentVersionId"
               AND NOT audit_logged_in_transaction('FileVersion', NEW."CurrentVersionId", 'FileVersionCreated') THEN
                PERFORM audit_write('File', NEW."Id", 'FileActiveVersionChanged', 'Update',
                    jsonb_build_object(
                        'old',            (SELECT "VersionNumber" FROM "FileVersions" WHERE "Id" = OLD."CurrentVersionId"),
                        'new',            (SELECT "VersionNumber" FROM "FileVersions" WHERE "Id" = NEW."CurrentVersionId"),
                        'old_version_id', OLD."CurrentVersionId",
                        'new_version_id', NEW."CurrentVersionId",
                        'name',           NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."MimeType" IS DISTINCT FROM NEW."MimeType" THEN
                PERFORM audit_write('File', NEW."Id", 'FileTypeChanged', 'Update',
                    jsonb_build_object('old', OLD."MimeType", 'new', NEW."MimeType", 'name', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

        ELSIF TG_OP = 'DELETE' THEN
            PERFORM audit_write('File', OLD."Id", 'FileDeletedPermanently', 'Delete',
                jsonb_build_object('name', OLD."Name", 'mime_type', OLD."MimeType"),
                OLD."OwnerId", NULL, OLD."Name");
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS files_audit_trigger ON "Files";

CREATE TRIGGER files_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "Name", "DirectoryId", "DeletedAt", "CurrentVersionId", "MimeType"
    ON "Files"
    FOR EACH ROW
    EXECUTE FUNCTION audit_files_changes();
