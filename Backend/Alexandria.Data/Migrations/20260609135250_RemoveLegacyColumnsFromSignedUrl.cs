using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyColumnsFromSignedUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignedUrls_BucketName_ObjectName",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "BucketName",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "ObjectName",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "Permission",
                table: "SignedUrls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                table: "SignedUrls",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObjectName",
                table: "SignedUrls",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Permission",
                table: "SignedUrls",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_BucketName_ObjectName",
                table: "SignedUrls",
                columns: new[] { "BucketName", "ObjectName" });
        }
    }
}
