using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RewriteAuditsToProperlyStoreIntend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MetadataJson",
                table: "AuditLogs",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventCode",
                table: "AuditLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FallbackDescription",
                table: "AuditLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventCode",
                table: "AuditLogs",
                column: "EventCode");

            // ----------------------------------------------------------------
            // STEP 1: Backfill EventCode from old Description patterns
            // ----------------------------------------------------------------
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs"" SET ""EventCode"" = CASE
                    -- File events
                    WHEN ""Description"" LIKE 'File % created'                          THEN 'FileCreated'
                    WHEN ""Description"" LIKE 'File renamed from%'                      THEN 'FileRenamed'
                    WHEN ""Description"" LIKE 'File % moved to different directory'     THEN 'FileMoved'
                    WHEN ""Description"" LIKE 'File % soft deleted'                     THEN 'FileSoftDeleted'
                    WHEN ""Description"" LIKE 'File % restored from deletion'           THEN 'FileRestored'
                    WHEN ""Description"" LIKE 'File % permanently deleted'              THEN 'FileDeletedPermanently'

                    -- User events
                    WHEN ""Description"" LIKE 'User % created'                          THEN 'UserCreated'
                    WHEN ""Description"" LIKE 'User % deleted'                          THEN 'UserDeleted'
                    WHEN ""Description"" LIKE 'Email changed for user%'                 THEN 'UserEmailChanged'
                    WHEN ""Description"" LIKE 'Password changed for user%'              THEN 'UserPasswordChanged'
                    WHEN ""Description"" LIKE 'Lockout status changed for user%'        THEN 'UserLockedOut'
                    WHEN ""Description"" LIKE 'Two-factor authentication enabled%'      THEN 'UserTwoFactorEnabled'
                    WHEN ""Description"" LIKE 'Two-factor authentication disabled%'     THEN 'UserTwoFactorDisabled'

                    -- Anything unrecognised gets a sentinel so we can spot it
                    ELSE 'Unknown'
                END
                WHERE ""EventCode"" IS NULL;
            ");

            // ----------------------------------------------------------------
            // STEP 2: Preserve the original English text as a fallback
            //         for records we couldn't map — keeps them readable
            // ----------------------------------------------------------------

            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""FallbackDescription"" = ""Description""
                WHERE ""EventCode"" = 'Unknown'
                  AND ""Description"" IS NOT NULL;
            ");

            // ----------------------------------------------------------------
            // STEP 3: Normalise MetadataJson keys to match the new flat shape
            //         Old triggers nested values under 'name_changed',
            //         'directory_changed' etc. — flatten them to 'old'/'new'
            // ----------------------------------------------------------------

            // FileRenamed: { name_changed: { old, new } } → { old, new }
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""MetadataJson"" = jsonb_build_object(
                    'old', ""MetadataJson"" -> 'name_changed' -> 'old',
                    'new', ""MetadataJson"" -> 'name_changed' -> 'new'
                )
                WHERE ""EventCode"" = 'FileRenamed'
                  AND ""MetadataJson"" ? 'name_changed';
            ");

            // FileMoved: { directory_changed: { old, new } } → { old_directory_id, new_directory_id }
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""MetadataJson"" = jsonb_build_object(
                    'old_directory_id', ""MetadataJson"" -> 'directory_changed' -> 'old',
                    'new_directory_id', ""MetadataJson"" -> 'directory_changed' -> 'new'
                )
                WHERE ""EventCode"" = 'FileMoved'
                  AND ""MetadataJson"" ? 'directory_changed';
            ");

            // FileSoftDeleted: remove the redundant 'soft_deleted: true' key
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""MetadataJson"" = ""MetadataJson"" - 'soft_deleted'
                WHERE ""EventCode"" = 'FileSoftDeleted'
                  AND ""MetadataJson"" ? 'soft_deleted';
            ");

            // FileRestored: remove the redundant 'restored: true' key
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""MetadataJson"" = ""MetadataJson"" - 'restored'
                WHERE ""EventCode"" = 'FileRestored'
                  AND ""MetadataJson"" ? 'restored';
            ");

            // UserLockedOut: { lockout_changed: { enabled, end } } → { enabled, end }
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs""
                SET ""MetadataJson"" = jsonb_build_object(
                    'enabled', ""MetadataJson"" -> 'lockout_changed' -> 'enabled',
                    'end',     ""MetadataJson"" -> 'lockout_changed' -> 'end'
                )
                WHERE ""EventCode"" = 'UserLockedOut'
                  AND ""MetadataJson"" ? 'lockout_changed';
            ");

            // ----------------------------------------------------------------
            // STEP 4: Warn about anything we couldn't map — don't silently lose it
            // ----------------------------------------------------------------
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_count INT;
                BEGIN
                    SELECT COUNT(*) INTO v_count
                    FROM ""AuditLogs""
                    WHERE ""EventCode"" = 'Unknown';

                    IF v_count > 0 THEN
                        RAISE WARNING
                            'MigrateAuditLogsToEventCodes: % row(s) could not be mapped to a known EventCode. '
                            'They have been kept with EventCode=''Unknown'' and their original text '
                            'preserved in FallbackDescription. Review and remap manually if needed.',
                            v_count;
                    END IF;
                END $$;
            ");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AuditLogs");

            // ----------------------------------------------------------------
            // STEP 5: Replace the file trigger function
            // ----------------------------------------------------------------
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
                                TG_OP,
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

            // ----------------------------------------------------------------
            // STEP 6: Replace the user trigger function
            // ----------------------------------------------------------------
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
                                TG_OP,
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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            // this restores the schema only — original Description values
            // cannot be recovered from EventCode. Restore from backup if data recovery is needed.

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EventCode",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EventCode",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "FallbackDescription",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "MetadataJson",
                table: "AuditLogs",
                type: "json",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AuditLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
