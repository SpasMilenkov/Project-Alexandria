-- The owner's files whose current version's document text matches a to_tsquery expression in
-- the simple configuration. Trashed files are included; the caller filters them, since the
-- search covers the trash too.
-- The planner prices a match against a stored vector as cheap and never counts loading it, so
-- once a query matches most documents it scans every vector instead of reading the index, which
-- answers the same question without opening them (630 ms against 17 ms for 3,000 documents).
-- A function with a SET clause is never inlined, so the setting covers this query only.
CREATE OR REPLACE FUNCTION matching_document_files(p_owner_id UUID, p_query TEXT)
    RETURNS TABLE
            (
                "FileId" UUID
            )
    LANGUAGE sql
    STABLE
    SET enable_seqscan = off
AS
$$
SELECT f."Id"
FROM "DocumentText" AS t
         JOIN "FileVersions" AS v ON v."ContentObjectId" = t."ContentObjectId"
         JOIN "Files" AS f ON f."CurrentVersionId" = v."Id"
WHERE t."DeletedAt" IS NULL
  AND f."OwnerId" = p_owner_id
  AND t."SearchVector" @@ to_tsquery('simple', p_query);
$$;
