using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPreviewToFileRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Previews_FileId",
                table: "Previews");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviewId",
                table: "Files",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_Previews_FileId",
                table: "Previews",
                column: "FileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Previews_FileId",
                table: "Previews");

            migrationBuilder.DropColumn(
                name: "PreviewId",
                table: "Files");

            migrationBuilder.CreateIndex(
                name: "IX_Previews_FileId",
                table: "Previews",
                column: "FileId");
        }
    }
}
