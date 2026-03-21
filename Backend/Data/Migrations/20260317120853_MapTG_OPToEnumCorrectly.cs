using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class MapTG_OPToEnumCorrectly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_files_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'FileCreated';
                            v_metadata   := jsonb_build_object(
                                'name',         NEW.""Name"",
                                'mime_type',    NEW.""MimeType"",
                                'directory_id', NEW.""DirectoryId"",
                                'owner_id',     NEW.""OwnerId""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object();

                            IF OLD.""Name"" IS DISTINCT FROM NEW.""Name"" THEN
                                v_event_code := 'FileRenamed';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'old', OLD.""Name"",
                                    'new', NEW.""Name""
                                );
                            END IF;

                            IF OLD.""DirectoryId"" IS DISTINCT FROM NEW.""DirectoryId"" THEN
                                v_event_code := COALESCE(v_event_code, 'FileMoved');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'old_directory_id', OLD.""DirectoryId"",
                                    'new_directory_id', NEW.""DirectoryId"",
                                    'name',             NEW.""Name""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_event_code := COALESCE(v_event_code, 'FileSoftDeleted');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name',       NEW.""Name"",
                                    'deleted_at', NEW.""DeletedAt""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_event_code := COALESCE(v_event_code, 'FileRestored');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name', NEW.""Name""
                                );
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'FileDeletedPermanently';
                            v_metadata   := jsonb_build_object(
                                'name',      OLD.""Name"",
                                'mime_type', OLD.""MimeType"",
                                'owner_id',  OLD.""OwnerId""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'File',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_users_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'UserCreated';
                            v_metadata   := jsonb_build_object(
                                'username',        NEW.""UserName"",
                                'email',           NEW.""Email"",
                                'email_confirmed', NEW.""EmailConfirmed""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object();

                            IF OLD.""Email"" IS DISTINCT FROM NEW.""Email"" THEN
                                v_event_code := 'UserEmailChanged';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'old', OLD.""Email"",
                                    'new', NEW.""Email""
                                );
                            END IF;

                            IF OLD.""PasswordHash"" IS DISTINCT FROM NEW.""PasswordHash"" THEN
                                v_event_code := COALESCE(v_event_code, 'UserPasswordChanged');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'changed_at', clock_timestamp()
                                );
                            END IF;

                            IF OLD.""LockoutEnabled"" != NEW.""LockoutEnabled"" OR
                               OLD.""LockoutEnd"" IS DISTINCT FROM NEW.""LockoutEnd"" THEN
                                v_event_code := COALESCE(v_event_code, 'UserLockedOut');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'enabled', NEW.""LockoutEnabled"",
                                    'end',     NEW.""LockoutEnd""
                                );
                            END IF;

                            IF OLD.""TwoFactorEnabled"" != NEW.""TwoFactorEnabled"" THEN
                                v_event_code := COALESCE(v_event_code,
                                    CASE WHEN NEW.""TwoFactorEnabled""
                                        THEN 'UserTwoFactorEnabled'
                                        ELSE 'UserTwoFactorDisabled'
                                    END
                                );
                                v_metadata := v_metadata || jsonb_build_object(
                                    'enabled', NEW.""TwoFactorEnabled""
                                );
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'UserDeleted';
                            v_metadata   := jsonb_build_object(
                                'username', OLD.""UserName"",
                                'email',    OLD.""Email""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'User',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_directories_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'DirectoryCreated';
                            v_metadata   := jsonb_build_object(
                                'name',      NEW.""Name"",
                                'parent_id', NEW.""ParentId"",
                                'owner_id',  NEW.""OwnerId""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object();

                            IF OLD.""Name"" IS DISTINCT FROM NEW.""Name"" THEN
                                v_event_code := 'DirectoryRenamed';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'old', OLD.""Name"",
                                    'new', NEW.""Name""
                                );
                            END IF;

                            IF OLD.""ParentId"" IS DISTINCT FROM NEW.""ParentId"" THEN
                                v_event_code := COALESCE(v_event_code, 'DirectoryMoved');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name',          NEW.""Name"",
                                    'old_parent_id', OLD.""ParentId"",
                                    'new_parent_id', NEW.""ParentId""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_event_code := COALESCE(v_event_code, 'DirectorySoftDeleted');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name',       NEW.""Name"",
                                    'deleted_at', NEW.""DeletedAt""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_event_code := COALESCE(v_event_code, 'DirectoryRestored');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name', NEW.""Name""
                                );
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'DirectoryDeletedPermanently';
                            v_metadata   := jsonb_build_object(
                                'name',      OLD.""Name"",
                                'parent_id', OLD.""ParentId"",
                                'owner_id',  OLD.""OwnerId""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'Directory',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE TRIGGER directories_audit_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON ""Directories""
                    FOR EACH ROW
                    EXECUTE FUNCTION audit_directories_changes();
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_file_versions_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'FileVersionCreated';
                            v_metadata   := jsonb_build_object(
                                'file_id',        NEW.""FileId"",
                                'version_number', NEW.""VersionNumber"",
                                'mime_type',      NEW.""MimeType"",
                                'size',           NEW.""Size""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object(
                                'file_id',        NEW.""FileId"",
                                'version_number', NEW.""VersionNumber""
                            );

                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_event_code := 'FileVersionSoftDeleted';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'deleted_at', NEW.""DeletedAt""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_event_code := 'FileVersionRestored';
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'FileVersionDeletedPermanently';
                            v_metadata   := jsonb_build_object(
                                'file_id',        OLD.""FileId"",
                                'version_number', OLD.""VersionNumber"",
                                'mime_type',      OLD.""MimeType"",
                                'size',           OLD.""Size""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'FileVersion',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE TRIGGER file_versions_audit_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON ""FileVersions""
                    FOR EACH ROW
                    EXECUTE FUNCTION audit_file_versions_changes();
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_previews_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'PreviewCreated';
                            v_metadata   := jsonb_build_object(
                                'name',      NEW.""Name"",
                                'mime_type', NEW.""MimeType"",
                                'file_id',   NEW.""FileId""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object(
                                'name',    NEW.""Name"",
                                'file_id', NEW.""FileId""
                            );

                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_event_code := 'PreviewSoftDeleted';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'deleted_at', NEW.""DeletedAt""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_event_code := 'PreviewRestored';
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'PreviewDeletedPermanently';
                            v_metadata   := jsonb_build_object(
                                'name',      OLD.""Name"",
                                'mime_type', OLD.""MimeType"",
                                'file_id',   OLD.""FileId""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'Preview',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE TRIGGER previews_audit_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON ""Previews""
                    FOR EACH ROW
                    EXECUTE FUNCTION audit_previews_changes();
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_tags_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id    UUID;
                    v_ip_address TEXT;
                    v_metadata   JSONB;
                    v_event_code TEXT;
                BEGIN
                    BEGIN
                        v_user_id := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
                    EXCEPTION WHEN OTHERS THEN
                        v_user_id := NULL;
                    END;

                    BEGIN
                        v_ip_address := NULLIF(current_setting('app.current_ip', true), '');
                    EXCEPTION WHEN OTHERS THEN
                        v_ip_address := NULL;
                    END;

                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            v_event_code := 'TagCreated';
                            v_metadata   := jsonb_build_object(
                                'name',        NEW.""Name"",
                                'icon',        NEW.""Icon"",
                                'color',       NEW.""Color"",
                                'description', NEW.""Description"",
                                'owner_id',    NEW.""OwnerId""
                            );

                        ELSIF TG_OP = 'UPDATE' THEN
                            v_metadata := jsonb_build_object();

                            IF OLD.""Name"" IS DISTINCT FROM NEW.""Name"" THEN
                                v_event_code := 'TagRenamed';
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'old', OLD.""Name"",
                                    'new', NEW.""Name""
                                );
                            END IF;

                            IF OLD.""Color"" IS DISTINCT FROM NEW.""Color"" OR
                               OLD.""Icon""  IS DISTINCT FROM NEW.""Icon""  OR
                               OLD.""Description"" IS DISTINCT FROM NEW.""Description"" THEN
                                v_event_code := COALESCE(v_event_code, 'TagUpdated');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name',                NEW.""Name"",
                                    'color_changed',       OLD.""Color"" IS DISTINCT FROM NEW.""Color"",
                                    'icon_changed',        OLD.""Icon""  IS DISTINCT FROM NEW.""Icon"",
                                    'description_changed', OLD.""Description"" IS DISTINCT FROM NEW.""Description""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_event_code := COALESCE(v_event_code, 'TagSoftDeleted');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name',       NEW.""Name"",
                                    'deleted_at', NEW.""DeletedAt""
                                );
                            END IF;

                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_event_code := COALESCE(v_event_code, 'TagRestored');
                                v_metadata   := v_metadata || jsonb_build_object(
                                    'name', NEW.""Name""
                                );
                            END IF;

                        ELSIF TG_OP = 'DELETE' THEN
                            v_event_code := 'TagDeletedPermanently';
                            v_metadata   := jsonb_build_object(
                                'name',     OLD.""Name"",
                                'owner_id', OLD.""OwnerId""
                            );
                        END IF;

                        IF v_event_code IS NOT NULL THEN
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""EventCode"", ""MetadataJson"", ""FallbackDescription"",
                                ""Timestamp"", ""IpAddress"", ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                CASE TG_OP
                                    WHEN 'INSERT' THEN 'Create'
                                    WHEN 'UPDATE' THEN 'Update'
                                    WHEN 'DELETE' THEN 'Delete'
                                END,
                                v_user_id,
                                'Tag',
                                COALESCE(NEW.""Id"", OLD.""Id""),
                                v_event_code,
                                v_metadata,
                                NULL,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;

                    EXCEPTION WHEN OTHERS THEN
                        RAISE WARNING 'Audit logging failed for % %: %', TG_TABLE_NAME, TG_OP, SQLERRM;
                    END;

                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE TRIGGER tags_audit_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON ""Tags""
                    FOR EACH ROW
                    EXECUTE FUNCTION audit_tags_changes();
            ");
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
