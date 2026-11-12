CREATE OR REPLACE FUNCTION audit_directories_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    BEGIN
        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('Directory', NEW."Id", 'DirectoryCreated', 'Create',
                jsonb_build_object('name', NEW."Name", 'parent_id', NEW."ParentId"),
                NEW."OwnerId", NULL, NEW."Name");

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."Name" IS DISTINCT FROM NEW."Name" THEN
                PERFORM audit_write('Directory', NEW."Id", 'DirectoryRenamed', 'Update',
                    jsonb_build_object('old', OLD."Name", 'new', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."ParentId" IS DISTINCT FROM NEW."ParentId" THEN
                PERFORM audit_write('Directory', NEW."Id", 'DirectoryMoved', 'Update',
                    jsonb_build_object(
                        'old',              audit_directory_name(OLD."ParentId"),
                        'new',              audit_directory_name(NEW."ParentId"),
                        'old_directory_id', OLD."ParentId",
                        'new_directory_id', NEW."ParentId",
                        'name',             NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('Directory', NEW."Id", 'DirectorySoftDeleted', 'Delete',
                    jsonb_build_object('name', NEW."Name", 'deleted_at', NEW."DeletedAt"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                PERFORM audit_write('Directory', NEW."Id", 'DirectoryRestored', 'Update',
                    jsonb_build_object('name', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

        ELSIF TG_OP = 'DELETE' THEN
            PERFORM audit_write('Directory', OLD."Id", 'DirectoryDeletedPermanently', 'Delete',
                jsonb_build_object('name', OLD."Name", 'parent_id', OLD."ParentId"),
                OLD."OwnerId", NULL, OLD."Name");
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS directories_audit_trigger ON "Directories";

CREATE TRIGGER directories_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "Name", "ParentId", "DeletedAt"
    ON "Directories"
    FOR EACH ROW
    EXECUTE FUNCTION audit_directories_changes();
