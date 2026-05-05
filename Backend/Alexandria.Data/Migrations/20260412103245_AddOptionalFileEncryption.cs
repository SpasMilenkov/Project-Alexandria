using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalFileEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionHint",
                table: "FileVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptionIv",
                table: "FileVersions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptionSalt",
                table: "FileVersions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "IntegrityTag",
                table: "FileVersions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEncrypted",
                table: "FileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_EncryptionFields_Consistency",
                table: "FileVersions",
                sql: "(\"IsEncrypted\" = false AND \"EncryptionIv\" IS NULL AND \"EncryptionSalt\" IS NULL AND \"IntegrityTag\" IS NULL)\n              OR\n              (\"IsEncrypted\" = true AND \"EncryptionIv\" IS NOT NULL AND \"EncryptionSalt\" IS NOT NULL AND \"IntegrityTag\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_EncryptionIv_Length",
                table: "FileVersions",
                sql: "\"EncryptionIv\" IS NULL OR octet_length(\"EncryptionIv\") = 12");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions",
                sql: "\"EncryptionSalt\" IS NULL OR octet_length(\"EncryptionSalt\") = 32");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileEntry_IntegrityTag_Length",
                table: "FileVersions",
                sql: "\"IntegrityTag\" IS NULL OR octet_length(\"IntegrityTag\") = 16");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_EncryptionFields_Consistency",
                table: "FileVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_EncryptionIv_Length",
                table: "FileVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_EncryptionSalt_Length",
                table: "FileVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileEntry_IntegrityTag_Length",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "EncryptionHint",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "EncryptionIv",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "EncryptionSalt",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "IntegrityTag",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "IsEncrypted",
                table: "FileVersions");
        }
    }
}
