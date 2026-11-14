-- A file adds its current version to its folder, so a folder sums to the sizes the explorer
-- lists for the files inside it. The current version is trusted even while soft-deleted,
-- because restoring a file and its versions are separate statements and the file trigger
-- would otherwise read the version before it comes back.
CREATE OR REPLACE FUNCTION refresh_directory_direct_files_size(p_directory_ids UUID[])
    RETURNS VOID
    LANGUAGE plpgsql
AS
$$
BEGIN
    IF p_directory_ids IS NULL OR cardinality(p_directory_ids) = 0 THEN
        RETURN;
    END IF;

    -- Two uploads into one folder would otherwise each sum without the other's uncommitted file,
    -- and the later write would drop the earlier one. Locking first makes the second writer
    -- wait, and the sum below is a new statement, so it reads what the first one committed.
    -- Id order keeps bulk statements that share folders from deadlocking.
    PERFORM 1
    FROM "Directories"
    WHERE "Id" = ANY (p_directory_ids)
    ORDER BY "Id"
        FOR NO KEY UPDATE;

    UPDATE "Directories" d
    SET "DirectFilesSize" = s.size
    FROM (SELECT ids.directory_id,
                 COALESCE((SELECT SUM(v."Size")
                           FROM "Files" f
                                    JOIN "FileVersions" v ON v."Id" = f."CurrentVersionId"
                           WHERE f."DirectoryId" = ids.directory_id
                             AND f."DeletedAt" IS NULL), 0)::BIGINT AS size
          FROM (SELECT DISTINCT unnest(p_directory_ids) AS directory_id) ids) s
    WHERE d."Id" = s.directory_id
      AND d."DirectFilesSize" IS DISTINCT FROM s.size;
END;
$$;
