using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveToVersionBasedPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Previews_Files_FileId",
                table: "Previews");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Previews");
            
            // drop previews that can't be matched to any existing version by hash,
            // these were orphaned/stale even under the old file-bound scheme
            migrationBuilder.Sql(@"
                DELETE FROM ""Previews"" p
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""FileVersions"" fv
                    WHERE fv.""FileId"" = p.""FileId""
                      AND encode(fv.""ContentHash"", 'hex') = split_part(p.""Path"", '/', 2)
                );
            ");

            // repoint remaining rows: FileId column becomes VersionId next,
            // overwrite its value with the matching FileVersion.Id now while Path is still around
            migrationBuilder.Sql(@"
                UPDATE ""Previews"" p
                SET ""FileId"" = fv.""Id""
                FROM ""FileVersions"" fv
                WHERE fv.""FileId"" = p.""FileId""
                  AND encode(fv.""ContentHash"", 'hex') = split_part(p.""Path"", '/', 2);
            ");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Previews");

            migrationBuilder.DropColumn(
                name: "HasPreview",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "PreviewGeneratedAt",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "PreviewId",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "FileId",
                table: "Previews",
                newName: "VersionId");

            // the renamed index is still single-column unique (VersionId only), a leftover
            // from the file-bound era, it would block a version from ever having both a
            // Preview and a Thumbnail row, drop it before creating the composite index
            migrationBuilder.DropIndex(
                name: "IX_Previews_FileId",
                table: "Previews");
            
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Previews",
                type: "text",
                nullable: false,
                defaultValue: "Preview");

            migrationBuilder.CreateIndex(
                name: "IX_Previews_VersionId_Kind",
                table: "Previews",
                columns: new[] { "VersionId", "Kind" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Previews_FileVersions_VersionId",
                table: "Previews",
                column: "VersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Previews_FileVersions_VersionId",
                table: "Previews");

            migrationBuilder.DropIndex(
                name: "IX_Previews_VersionId_Kind",
                table: "Previews");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Previews");

            migrationBuilder.RenameColumn(
                name: "VersionId",
                table: "Previews",
                newName: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Previews_FileId",
                table: "Previews",
                column: "FileId",
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Previews",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Previews",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasPreview",
                table: "Files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviewGeneratedAt",
                table: "Files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviewId",
                table: "Files",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddForeignKey(
                name: "FK_Previews_Files_FileId",
                table: "Previews",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
