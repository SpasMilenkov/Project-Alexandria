using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionSharingToSignedUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileVersionId",
                table: "SignedUrls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_FileVersionId",
                table: "SignedUrls",
                column: "FileVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls");

            migrationBuilder.DropIndex(
                name: "IX_SignedUrls_FileVersionId",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "FileVersionId",
                table: "SignedUrls");
        }
    }
}
