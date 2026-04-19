using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AdjustHashLengthRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions",
                sql: "\"EncryptionSalt\" IS NULL OR octet_length(\"EncryptionSalt\") = 16");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions",
                sql: "\"EncryptionSalt\" IS NULL OR octet_length(\"EncryptionSalt\") = 32");
        }
    }
}
