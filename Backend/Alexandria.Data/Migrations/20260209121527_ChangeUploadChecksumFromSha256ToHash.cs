using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUploadChecksumFromSha256ToHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Upload_Sha256_Length",
                table: "Uploads");

            migrationBuilder.RenameColumn(
                name: "Sha256",
                table: "Uploads",
                newName: "Hash");

            migrationBuilder.RenameIndex(
                name: "IX_Uploads_Sha256",
                table: "Uploads",
                newName: "IX_Uploads_Hash");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Upload_Hash_Length",
                table: "Uploads",
                sql: "octet_length(\"Hash\") IN (16, 32, 64)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Upload_Hash_Length",
                table: "Uploads");

            migrationBuilder.RenameColumn(
                name: "Hash",
                table: "Uploads",
                newName: "Sha256");

            migrationBuilder.RenameIndex(
                name: "IX_Uploads_Hash",
                table: "Uploads",
                newName: "IX_Uploads_Sha256");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Upload_Sha256_Length",
                table: "Uploads",
                sql: "octet_length(\"Sha256\") = 32");
        }
    }
}
