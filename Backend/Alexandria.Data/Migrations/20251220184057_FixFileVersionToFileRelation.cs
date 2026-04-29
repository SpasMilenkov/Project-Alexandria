using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileVersionToFileRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentVersionId",
                table: "Files",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects",
                column: "Hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentVersionId",
                table: "Files",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects",
                column: "Hash");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
