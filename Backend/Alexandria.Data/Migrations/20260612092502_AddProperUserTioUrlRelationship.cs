using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProperUserTioUrlRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls");

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_CreatorId",
                table: "SignedUrls",
                column: "CreatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_SignedUrls_AspNetUsers_CreatorId",
                table: "SignedUrls",
                column: "CreatorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SignedUrls_AspNetUsers_CreatorId",
                table: "SignedUrls");

            migrationBuilder.DropForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls");

            migrationBuilder.DropIndex(
                name: "IX_SignedUrls_CreatorId",
                table: "SignedUrls");

            migrationBuilder.AddForeignKey(
                name: "FK_SignedUrls_FileVersions_FileVersionId",
                table: "SignedUrls",
                column: "FileVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id");
        }
    }
}
