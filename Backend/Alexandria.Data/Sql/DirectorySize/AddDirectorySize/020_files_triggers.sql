-- Statement-level triggers with transition tables refresh each touched folder once per
-- statement, so trashing a folder with thousands of files recomputes it once, not per file.
-- Transition tables rule out an UPDATE OF column list, so the update trigger filters the
-- rows whose folder, trash state or current version actually changed.

-- A file is inserted before its first version exists, so rows without a current version add
-- nothing yet and the follow-up update that sets it does the refresh.
CREATE OR REPLACE FUNCTION files_directory_size_after_insert()
    RETURNS TRIGGER
    LANGUAGE plpgsql
AS
$$
BEGIN
    PERFORM refresh_directory_direct_files_size(ARRAY(
            SELECT DISTINCT "DirectoryId"
            FROM new_files
            WHERE "DirectoryId" IS NOT NULL
              AND "DeletedAt" IS NULL
              AND "CurrentVersionId" IS NOT NULL));

    RETURN NULL;
END;
$$;

-- A move refreshes both the folder the file left and the one it entered.
CREATE OR REPLACE FUNCTION files_directory_size_after_update()
    RETURNS TRIGGER
    LANGUAGE plpgsql
AS
$$
BEGIN
    PERFORM refresh_directory_direct_files_size(ARRAY(
            SELECT DISTINCT changed.directory_id
            FROM old_files o
                     JOIN new_files n ON n."Id" = o."Id"
                     CROSS JOIN LATERAL (VALUES (o."DirectoryId"), (n."DirectoryId")) AS changed(directory_id)
            WHERE changed.directory_id IS NOT NULL
              AND (o."DirectoryId", o."DeletedAt", o."CurrentVersionId") IS DISTINCT FROM (n."DirectoryId", n."DeletedAt", n."CurrentVersionId")));

    RETURN NULL;
END;
$$;

-- Purging a file that was already in the trash changes nothing, so only live rows count.
CREATE OR REPLACE FUNCTION files_directory_size_after_delete()
    RETURNS TRIGGER
    LANGUAGE plpgsql
AS
$$
BEGIN
    PERFORM refresh_directory_direct_files_size(ARRAY(
            SELECT DISTINCT "DirectoryId"
            FROM old_files
            WHERE "DirectoryId" IS NOT NULL
              AND "DeletedAt" IS NULL
              AND "CurrentVersionId" IS NOT NULL));

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS files_directory_size_insert_trigger ON "Files";
DROP TRIGGER IF EXISTS files_directory_size_update_trigger ON "Files";
DROP TRIGGER IF EXISTS files_directory_size_delete_trigger ON "Files";

CREATE TRIGGER files_directory_size_insert_trigger
    AFTER INSERT
    ON "Files"
    REFERENCING NEW TABLE AS new_files
    FOR EACH STATEMENT
EXECUTE FUNCTION files_directory_size_after_insert();

CREATE TRIGGER files_directory_size_update_trigger
    AFTER UPDATE
    ON "Files"
    REFERENCING OLD TABLE AS old_files NEW TABLE AS new_files
    FOR EACH STATEMENT
EXECUTE FUNCTION files_directory_size_after_update();

CREATE TRIGGER files_directory_size_delete_trigger
    AFTER DELETE
    ON "Files"
    REFERENCING OLD TABLE AS old_files
    FOR EACH STATEMENT
EXECUTE FUNCTION files_directory_size_after_delete();
