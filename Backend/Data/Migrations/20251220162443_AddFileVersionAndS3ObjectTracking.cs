using System;
using System.Globalization;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersionAndS3ObjectTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "Files");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentVersionId",
                table: "Files",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateTable(
                name: "ContentObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    RefCount = table.Column<int>(type: "integer", nullable: false),
                    OrphanedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Size = table.Column<BigInteger>(type: "numeric(20,0)", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MimeType = table.Column<string>(type: "varchar(255)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true),
                    ContentObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileVersions_ContentObjects_ContentObjectId",
                        column: x => x.ContentObjectId,
                        principalTable: "ContentObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentObjects_Hash",
                table: "ContentObjects",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_ContentHash",
                table: "FileVersions",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_ContentObjectId",
                table: "FileVersions",
                column: "ContentObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_CreatedAt",
                table: "FileVersions",
                column: "CreatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_FileVersions_CurrentVersionId",
                table: "Files");

            migrationBuilder.DropTable(
                name: "FileVersions");

            migrationBuilder.DropTable(
                name: "ContentObjects");

            migrationBuilder.DropIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "CurrentVersionId",
                table: "Files");

            migrationBuilder.AddColumn<BigInteger>(
                name: "Size",
                table: "Files",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: BigInteger.Parse("0", NumberFormatInfo.InvariantInfo));
        }
    }
}
