using System;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class FilesAndSignedLinksTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    MimeType = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Size = table.Column<BigInteger>(type: "numeric(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HasPreview = table.Column<bool>(type: "boolean", nullable: false),
                    PreviewGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignedUrls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ObjectName = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    Token = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    Permission = table.Column<string>(type: "varchar(50)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignedUrls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignedUrls_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_CreatedAt",
                table: "Files",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_BucketName_ObjectName",
                table: "SignedUrls",
                columns: new[] { "BucketName", "ObjectName" });

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_ExpiresAt",
                table: "SignedUrls",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignedUrls_FileId",
                table: "SignedUrls",
                column: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignedUrls");

            migrationBuilder.DropTable(
                name: "Files");
        }
    }
}
