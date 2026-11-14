-- Totals are summed on read instead of stored per folder: a move then costs nothing, and an
-- upload locks one folder row instead of every folder up to the root. The walk only visits
-- folders, never files. UNION rather than UNION ALL ends the walk even if a cycle ever slips
-- into the tree. The high cost tells the planner to run it after LIMIT, for the page only.
CREATE OR REPLACE FUNCTION directory_tree_size(p_directory_id UUID)
    RETURNS BIGINT
    LANGUAGE sql
    STABLE
    PARALLEL SAFE
    COST 1000
AS
$$
WITH RECURSIVE subtree AS (SELECT "Id", "DirectFilesSize"
                           FROM "Directories"
                           WHERE "Id" = p_directory_id
                             AND "DeletedAt" IS NULL

                           UNION

                           SELECT d."Id", d."DirectFilesSize"
                           FROM "Directories" d
                                    JOIN subtree s ON d."ParentId" = s."Id"
                           WHERE d."DeletedAt" IS NULL)
SELECT COALESCE(SUM("DirectFilesSize"), 0)::BIGINT
FROM subtree;
$$;
