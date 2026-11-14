-- One pass over existing data. The Directories audit trigger only watches name, parent and
-- trash columns, so this leaves no audit rows behind.
UPDATE "Directories" d
SET "DirectFilesSize" = s.size
FROM (SELECT f."DirectoryId", SUM(v."Size")::BIGINT AS size
      FROM "Files" f
               JOIN "FileVersions" v ON v."Id" = f."CurrentVersionId"
      WHERE f."DirectoryId" IS NOT NULL
        AND f."DeletedAt" IS NULL
      GROUP BY f."DirectoryId") s
WHERE d."Id" = s."DirectoryId";
