using System;
using Alexandria.Data.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAuditLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsEnriched",
                table: "AuditLogs");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByIp",
                table: "RefreshTokens",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionStartedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "RefreshTokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "AuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentEntityId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OwnerId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "OwnerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ParentEntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "ParentEntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.Sql(@"
                UPDATE ""RefreshTokens"" SET ""SessionStartedAt"" = ""CreatedAt"";
            ");

            // Shared audit core, every trigger and the backfill of the new columns. The scripts
            // live in Sql/Audit/ExpandAuditLogging and run in name order.
            foreach (var script in EmbeddedSql.ReadFolder("Audit.ExpandAuditLogging"))
                migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The rewritten core triggers stay in place: their previous bodies wrote to columns this
            // migration removes or that no longer exist. Only the triggers this migration added go.
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS signed_urls_audit_trigger ON ""SignedUrls"";
                DROP TRIGGER IF EXISTS user_roles_audit_trigger ON ""AspNetUserRoles"";
                DROP TRIGGER IF EXISTS file_tags_audit_trigger ON ""FileTags"";
                DROP TRIGGER IF EXISTS playlists_audit_trigger ON ""Playlists"";
                DROP TRIGGER IF EXISTS playlist_items_insert_audit_trigger ON ""PlaylistItems"";
                DROP TRIGGER IF EXISTS playlist_items_update_audit_trigger ON ""PlaylistItems"";
                DROP TRIGGER IF EXISTS playlist_items_delete_audit_trigger ON ""PlaylistItems"";
                DROP TRIGGER IF EXISTS directory_policies_audit_trigger ON ""DirectoryPolicies"";
                DROP TRIGGER IF EXISTS policy_rules_audit_trigger ON ""PolicyRules"";
                DROP TRIGGER IF EXISTS admin_settings_audit_trigger ON ""AdminSettings"";
                DROP TRIGGER IF EXISTS settings_audit_trigger ON ""Settings"";

                DROP FUNCTION IF EXISTS audit_signed_urls_changes();
                DROP FUNCTION IF EXISTS audit_user_roles_changes();
                DROP FUNCTION IF EXISTS audit_file_tags_changes();
                DROP FUNCTION IF EXISTS audit_playlists_changes();
                DROP FUNCTION IF EXISTS audit_playlist_items_inserted();
                DROP FUNCTION IF EXISTS audit_playlist_items_updated();
                DROP FUNCTION IF EXISTS audit_playlist_items_deleted();
                DROP FUNCTION IF EXISTS audit_playlist_items_write(JSONB);
                DROP FUNCTION IF EXISTS audit_directory_policies_changes();
                DROP FUNCTION IF EXISTS audit_policy_rules_changes();
                DROP FUNCTION IF EXISTS audit_settings_changes();
            ");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OwnerId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ParentEntityId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedByIp",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionStartedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ParentEntityId",
                table: "AuditLogs");

            // UserId stays NOT NULL: the database already had that constraint before this
            // migration, only the model claimed otherwise.

            migrationBuilder.AddColumn<bool>(
                name: "IsEnriched",
                table: "AuditLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }
    }
}
