using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRefCountForTrackingAliveContentObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileVersions_ContentObjectId",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects");

            migrationBuilder.DropColumn(
                name: "RefCount",
                table: "ContentObjects");

            migrationBuilder.CreateIndex(
                name: "ix_fileversions_contentobjectid_deletedat",
                table: "FileVersions",
                columns: new[] { "ContentObjectId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_contentobjects_hash_active",
                table: "ContentObjects",
                column: "Hash",
                unique: true,
                filter: "\"OrphanedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fileversions_contentobjectid_deletedat",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "ix_contentobjects_hash_active",
                table: "ContentObjects");

            migrationBuilder.AddColumn<int>(
                name: "RefCount",
                table: "ContentObjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_ContentObjectId",
                table: "FileVersions",
                column: "ContentObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects",
                column: "Hash",
                unique: true);
        }
    }
}
