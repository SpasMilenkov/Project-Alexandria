CREATE OR REPLACE FUNCTION audit_tags_changes()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    BEGIN
        IF TG_OP = 'INSERT' THEN
            PERFORM audit_write('Tag', NEW."Id", 'TagCreated', 'Create',
                jsonb_build_object(
                    'name',        NEW."Name",
                    'icon',        NEW."Icon",
                    'color',       NEW."Color",
                    'description', NEW."Description",
                    'facet',       NEW."Facet",
                    'parent_id',   NEW."ParentId"),
                NEW."OwnerId", NULL, NEW."Name");

        ELSIF TG_OP = 'UPDATE' THEN
            IF OLD."Name" IS DISTINCT FROM NEW."Name" THEN
                PERFORM audit_write('Tag', NEW."Id", 'TagRenamed', 'Update',
                    jsonb_build_object('old', OLD."Name", 'new', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."Color" IS DISTINCT FROM NEW."Color"
               OR OLD."Icon" IS DISTINCT FROM NEW."Icon"
               OR OLD."Description" IS DISTINCT FROM NEW."Description"
               OR OLD."Facet" IS DISTINCT FROM NEW."Facet" THEN
                PERFORM audit_write('Tag', NEW."Id", 'TagUpdated', 'Update',
                    jsonb_build_object(
                        'name',                NEW."Name",
                        'color_changed',       OLD."Color" IS DISTINCT FROM NEW."Color",
                        'icon_changed',        OLD."Icon" IS DISTINCT FROM NEW."Icon",
                        'description_changed', OLD."Description" IS DISTINCT FROM NEW."Description",
                        'facet_changed',       OLD."Facet" IS DISTINCT FROM NEW."Facet"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."ParentId" IS DISTINCT FROM NEW."ParentId" THEN
                PERFORM audit_write('Tag', NEW."Id", 'TagReparented', 'Update',
                    jsonb_build_object(
                        'old',           audit_tag_name(OLD."ParentId"),
                        'new',           audit_tag_name(NEW."ParentId"),
                        'old_parent_id', OLD."ParentId",
                        'new_parent_id', NEW."ParentId",
                        'name',          NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NULL AND NEW."DeletedAt" IS NOT NULL THEN
                PERFORM audit_write('Tag', NEW."Id", 'TagSoftDeleted', 'Delete',
                    jsonb_build_object('name', NEW."Name", 'deleted_at', NEW."DeletedAt"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

            IF OLD."DeletedAt" IS NOT NULL AND NEW."DeletedAt" IS NULL THEN
                PERFORM audit_write('Tag', NEW."Id", 'TagRestored', 'Update',
                    jsonb_build_object('name', NEW."Name"),
                    NEW."OwnerId", NULL, NEW."Name");
            END IF;

        ELSIF TG_OP = 'DELETE' THEN
            PERFORM audit_write('Tag', OLD."Id", 'TagDeletedPermanently', 'Delete',
                jsonb_build_object('name', OLD."Name"),
                OLD."OwnerId", NULL, OLD."Name");
        END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
    END;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS tags_audit_trigger ON "Tags";

CREATE TRIGGER tags_audit_trigger
    AFTER INSERT OR DELETE OR UPDATE OF "Name", "Color", "Icon", "Description", "Facet", "ParentId", "DeletedAt"
    ON "Tags"
    FOR EACH ROW
    EXECUTE FUNCTION audit_tags_changes();
