using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // File trigger
            migrationBuilder.Sql(
                @"
                CREATE OR REPLACE FUNCTION audit_files_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    v_user_id UUID;
                    v_ip_address TEXT;
                    v_metadata JSONB;
                    v_description TEXT;
                BEGIN
                    -- Safely get session context (returns NULL if not set)
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
                    
                    -- Wrap entire audit logging in exception handler
                    BEGIN
                        IF TG_OP = 'INSERT' THEN
                            -- Only log non-sensitive fields
                            v_metadata := jsonb_build_object(
                                'name', NEW.""Name"",
                                'mime_type', NEW.""MimeType"",
                                'directory_id', NEW.""DirectoryId"",
                                'owner_id', NEW.""OwnerId""
                            );
                            
                            v_description := 'File ""' || NEW.""Name"" || '"" created';
                            
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                'Create',
                                v_user_id,
                                'File',
                                NEW.""Id"",
                                v_description,
                                v_metadata,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                            
                        ELSIF TG_OP = 'UPDATE' THEN
                            -- Build metadata with only changed, non-sensitive fields
                            v_metadata := jsonb_build_object();
                            
                            -- Track what changed (whitelist approach)
                            IF OLD.""Name"" IS DISTINCT FROM NEW.""Name"" THEN
                                v_metadata := v_metadata || jsonb_build_object(
                                    'name_changed', jsonb_build_object(
                                        'old', OLD.""Name"",
                                        'new', NEW.""Name""
                                    )
                                );
                                v_description := 'File renamed from ""' || OLD.""Name"" || '"" to ""' || NEW.""Name"" || '""';
                            END IF;
                            
                            IF OLD.""DirectoryId"" IS DISTINCT FROM NEW.""DirectoryId"" THEN
                                v_metadata := v_metadata || jsonb_build_object(
                                    'directory_changed', jsonb_build_object(
                                        'old', OLD.""DirectoryId"",
                                        'new', NEW.""DirectoryId""
                                    )
                                );
                                IF v_description IS NULL THEN
                                    v_description := 'File ""' || NEW.""Name"" || '"" moved to different directory';
                                END IF;
                            END IF;
                            
                            IF OLD.""DeletedAt"" IS NULL AND NEW.""DeletedAt"" IS NOT NULL THEN
                                v_metadata := v_metadata || jsonb_build_object(
                                    'soft_deleted', true,
                                    'deleted_at', NEW.""DeletedAt""
                                );
                                v_description := 'File ""' || NEW.""Name"" || '"" soft deleted';
                            END IF;
                            
                            IF OLD.""DeletedAt"" IS NOT NULL AND NEW.""DeletedAt"" IS NULL THEN
                                v_metadata := v_metadata || jsonb_build_object(
                                    'restored', true
                                );
                                v_description := 'File ""' || NEW.""Name"" || '"" restored from deletion';
                            END IF;
                            
                            -- Only log if something actually changed
                            IF v_description IS NOT NULL THEN
                                INSERT INTO ""AuditLogs"" (
                                    ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                    ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                    ""Source"", ""IsEnriched""
                                ) VALUES (
                                    gen_random_uuid(),
                                    'Update',
                                    v_user_id,
                                    'File',
                                    NEW.""Id"",
                                    v_description,
                                    v_metadata,
                                    clock_timestamp(),
                                    v_ip_address,
                                    'Trigger',
                                    v_user_id IS NOT NULL
                                );
                            END IF;
                            
                        ELSIF TG_OP = 'DELETE' THEN
                            -- Only log non-sensitive info about deleted record
                            v_metadata := jsonb_build_object(
                                'name', OLD.""Name"",
                                'mime_type', OLD.""MimeType"",
                                'owner_id', OLD.""OwnerId"",
                                'deleted_permanently', true
                            );
                            
                            v_description := 'File ""' || OLD.""Name"" || '"" permanently deleted';
                            
                            INSERT INTO ""AuditLogs"" (
                                ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                ""Source"", ""IsEnriched""
                            ) VALUES (
                                gen_random_uuid(),
                                'Delete',
                                v_user_id,
                                'File',
                                OLD.""Id"",
                                v_description,
                                v_metadata,
                                clock_timestamp(),
                                v_ip_address,
                                'Trigger',
                                v_user_id IS NOT NULL
                            );
                        END IF;
                        
                    EXCEPTION WHEN OTHERS THEN
                        -- Log the error but don't fail the transaction
                        RAISE WARNING 'Audit logging failed for table % operation %: %', 
                            TG_TABLE_NAME, TG_OP, SQLERRM;
                        -- Transaction continues normally
                    END;
                    
                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER files_audit_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON ""Files""
                    FOR EACH ROW
                    EXECUTE FUNCTION audit_files_changes();
                "
                );

            // User trigger
            migrationBuilder.Sql(
                @"
                    CREATE OR REPLACE FUNCTION audit_users_changes()
                    RETURNS TRIGGER AS $$
                    DECLARE
                        v_user_id UUID;
                        v_ip_address TEXT;
                        v_metadata JSONB;
                        v_description TEXT;
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
                                -- Never log passwords, security stamps, etc.
                                v_metadata := jsonb_build_object(
                                    'username', NEW.""UserName"",
                                    'email', NEW.""Email"",
                                    'email_confirmed', NEW.""EmailConfirmed""
                                );
                                
                                v_description := 'User ""' || NEW.""UserName"" || '"" created';
                                
                                INSERT INTO ""AuditLogs"" (
                                    ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                    ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                    ""Source"", ""IsEnriched""
                                ) VALUES (
                                    gen_random_uuid(),
                                    'Create',
                                    v_user_id,
                                    'User',
                                    NEW.""Id"",
                                    v_description,
                                    v_metadata,
                                    clock_timestamp(),
                                    v_ip_address,
                                    'Trigger',
                                    v_user_id IS NOT NULL
                                );
                                
                            ELSIF TG_OP = 'UPDATE' THEN
                                v_metadata := jsonb_build_object();
                                
                                -- Only track specific, non-sensitive changes
                                IF OLD.""Email"" IS DISTINCT FROM NEW.""Email"" THEN
                                    v_metadata := v_metadata || jsonb_build_object(
                                        'email_changed', jsonb_build_object(
                                            'old', OLD.""Email"",
                                            'new', NEW.""Email""
                                        )
                                    );
                                    v_description := 'Email changed for user ""' || NEW.""UserName"" || '""';
                                END IF;
                                
                                IF OLD.""PasswordHash"" IS DISTINCT FROM NEW.""PasswordHash"" THEN
                                    -- NEVER log password hashes, just that it changed
                                    v_metadata := v_metadata || jsonb_build_object(
                                        'password_changed', true,
                                        'changed_at', clock_timestamp()
                                    );
                                    IF v_description IS NULL THEN
                                        v_description := 'Password changed for user ""' || NEW.""UserName"" || '""';
                                    END IF;
                                END IF;
                                
                                IF OLD.""LockoutEnabled"" != NEW.""LockoutEnabled"" OR 
                                   OLD.""LockoutEnd"" IS DISTINCT FROM NEW.""LockoutEnd"" THEN
                                    v_metadata := v_metadata || jsonb_build_object(
                                        'lockout_changed', jsonb_build_object(
                                            'enabled', NEW.""LockoutEnabled"",
                                            'end', NEW.""LockoutEnd""
                                        )
                                    );
                                    IF v_description IS NULL THEN
                                        v_description := 'Lockout status changed for user ""' || NEW.""UserName"" || '""';
                                    END IF;
                                END IF;
                                
                                IF OLD.""TwoFactorEnabled"" != NEW.""TwoFactorEnabled"" THEN
                                    v_metadata := v_metadata || jsonb_build_object(
                                        'two_factor_changed', NEW.""TwoFactorEnabled""
                                    );
                                    IF v_description IS NULL THEN
                                        v_description := 'Two-factor authentication ' || 
                                            CASE WHEN NEW.""TwoFactorEnabled"" THEN 'enabled' ELSE 'disabled' END || 
                                            ' for user ""' || NEW.""UserName"" || '""';
                                    END IF;
                                END IF;
                                
                                IF v_description IS NOT NULL THEN
                                    INSERT INTO ""AuditLogs"" (
                                        ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                        ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                        ""Source"", ""IsEnriched""
                                    ) VALUES (
                                        gen_random_uuid(),
                                        'Update',
                                        v_user_id,
                                        'User',
                                        NEW.""Id"",
                                        v_description,
                                        v_metadata,
                                        clock_timestamp(),
                                        v_ip_address,
                                        'Trigger',
                                        v_user_id IS NOT NULL
                                    );
                                END IF;
                                
                            ELSIF TG_OP = 'DELETE' THEN
                                v_metadata := jsonb_build_object(
                                    'username', OLD.""UserName"",
                                    'email', OLD.""Email""
                                    -- NO sensitive data
                                );
                                
                                v_description := 'User ""' || OLD.""UserName"" || '"" deleted';
                                
                                INSERT INTO ""AuditLogs"" (
                                    ""Id"", ""OperationType"", ""UserId"", ""EntityType"", ""EntityId"",
                                    ""Description"", ""MetadataJson"", ""Timestamp"", ""IpAddress"",
                                    ""Source"", ""IsEnriched""
                                ) VALUES (
                                    gen_random_uuid(),
                                    'Delete',
                                    v_user_id,
                                    'User',
                                    OLD.""Id"",
                                    v_description,
                                    v_metadata,
                                    clock_timestamp(),
                                    v_ip_address,
                                    'Trigger',
                                    v_user_id IS NOT NULL
                                );
                            END IF;
                            
                        EXCEPTION WHEN OTHERS THEN
                            RAISE WARNING 'Audit logging failed for table % operation %: %', 
                                TG_TABLE_NAME, TG_OP, SQLERRM;
                        END;
                        
                        RETURN COALESCE(NEW, OLD);
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER users_audit_trigger
                        AFTER INSERT OR UPDATE OR DELETE ON ""AspNetUsers""
                        FOR EACH ROW
                        EXECUTE FUNCTION audit_users_changes();
                "
            );
            
            migrationBuilder.Sql(
                @"
                ALTER TABLE ""AuditLogs"" SET (autovacuum_enabled = true);                
                "
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //Not dropping triggers to preserve the historical integrity
        }
    }
}
