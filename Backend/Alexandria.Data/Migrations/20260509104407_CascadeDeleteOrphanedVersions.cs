using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteOrphanedVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
