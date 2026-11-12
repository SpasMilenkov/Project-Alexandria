-- One-off backfill for rows written before the owner, parent and name columns existed.

UPDATE "AuditLogs"
SET "OwnerId" = ("MetadataJson" ->> 'owner_id')::UUID
WHERE "OwnerId" IS NULL
  AND "MetadataJson" ->> 'owner_id' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';

UPDATE "AuditLogs"
SET "ParentEntityId" = ("MetadataJson" ->> 'file_id')::UUID
WHERE "ParentEntityId" IS NULL
  AND "EntityType" IN ('FileVersion', 'Preview')
  AND "MetadataJson" ->> 'file_id' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';

UPDATE "AuditLogs" a
SET "OwnerId" = f."OwnerId"
FROM "Files" f
WHERE a."OwnerId" IS NULL AND a."EntityType" = 'File' AND f."Id" = a."EntityId";

UPDATE "AuditLogs" a
SET "OwnerId" = f."OwnerId", "EntityName" = COALESCE(a."EntityName", f."Name")
FROM "Files" f
WHERE a."EntityType" IN ('FileVersion', 'Preview')
  AND f."Id" = a."ParentEntityId"
  AND (a."OwnerId" IS NULL OR a."EntityName" IS NULL);

UPDATE "AuditLogs" a
SET "OwnerId" = d."OwnerId"
FROM "Directories" d
WHERE a."OwnerId" IS NULL AND a."EntityType" = 'Directory' AND d."Id" = a."EntityId";

UPDATE "AuditLogs" a
SET "OwnerId" = t."OwnerId"
FROM "Tags" t
WHERE a."OwnerId" IS NULL AND a."EntityType" = 'Tag' AND t."Id" = a."EntityId";

UPDATE "AuditLogs"
SET "OwnerId" = "EntityId"
WHERE "OwnerId" IS NULL AND "EntityType" = 'User';

UPDATE "AuditLogs"
SET "EntityName" = left(COALESCE("MetadataJson" ->> 'name', "MetadataJson" ->> 'new', "MetadataJson" ->> 'username'), 1000)
WHERE "EntityName" IS NULL
  AND "EntityType" IN ('File', 'Directory', 'Tag', 'User');

UPDATE "AuditLogs" a
SET "EntityName" = u."UserName"
FROM "AspNetUsers" u
WHERE a."EntityName" IS NULL AND a."EntityType" = 'User' AND u."Id" = a."EntityId";

-- Moves carry directory names in old/new; ids stay under old_directory_id/new_directory_id.
-- Directories that no longer exist resolve to null.
UPDATE "AuditLogs"
SET "MetadataJson" = "MetadataJson" || jsonb_build_object(
        'old', audit_directory_name(("MetadataJson" ->> 'old_directory_id')::UUID),
        'new', audit_directory_name(("MetadataJson" ->> 'new_directory_id')::UUID))
WHERE "EventCode" = 'FileMoved'
  AND NOT ("MetadataJson" ? 'old');

UPDATE "AuditLogs"
SET "MetadataJson" = ("MetadataJson" - 'old_parent_id' - 'new_parent_id') || jsonb_build_object(
        'old',              audit_directory_name(("MetadataJson" ->> 'old_parent_id')::UUID),
        'new',              audit_directory_name(("MetadataJson" ->> 'new_parent_id')::UUID),
        'old_directory_id', "MetadataJson" -> 'old_parent_id',
        'new_directory_id', "MetadataJson" -> 'new_parent_id')
WHERE "EventCode" = 'DirectoryMoved'
  AND "MetadataJson" ? 'old_parent_id';

-- Moving something to trash reads as a delete to the person who did it, so soft deletes
-- written as updates by the earlier triggers and the bulk summary are reclassified.
UPDATE "AuditLogs"
SET "OperationType" = 'Delete'
WHERE "OperationType" = 'Update'
  AND "EventCode" IN ('FileSoftDeleted', 'FileVersionSoftDeleted', 'DirectorySoftDeleted',
                      'PreviewSoftDeleted', 'TagSoftDeleted', 'UserSoftDeleted',
                      'PlaylistSoftDeleted', 'FilesDeletedBulk');
