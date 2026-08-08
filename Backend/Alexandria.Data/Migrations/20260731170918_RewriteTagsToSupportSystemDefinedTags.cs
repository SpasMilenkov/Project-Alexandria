using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RewriteTagsToSupportSystemDefinedTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FileTags",
                table: "FileTags");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Tags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "FileTags",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<double>(
                name: "Confidence",
                table: "FileTags",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FileTags",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "FileTags",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FileTags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FileTags",
                table: "FileTags",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FileEnrichments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Analyzer = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Version = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileEnrichments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileEnrichments_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ParentId",
                table: "Tags",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTags_FileId",
                table: "FileTags",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTags_FileId_TagId",
                table: "FileTags",
                columns: new[] { "FileId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTags_Source",
                table: "FileTags",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_FileEnrichments_FileId",
                table: "FileEnrichments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileEnrichments_FileId_Analyzer_CreatedAt",
                table: "FileEnrichments",
                columns: new[] { "FileId", "Analyzer", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Tags_ParentId",
                table: "Tags",
                column: "ParentId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Tags_ParentId",
                table: "Tags");

            migrationBuilder.DropTable(
                name: "FileEnrichments");

            migrationBuilder.DropIndex(
                name: "IX_Tags_ParentId",
                table: "Tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FileTags",
                table: "FileTags");

            migrationBuilder.DropIndex(
                name: "IX_FileTags_FileId",
                table: "FileTags");

            migrationBuilder.DropIndex(
                name: "IX_FileTags_FileId_TagId",
                table: "FileTags");

            migrationBuilder.DropIndex(
                name: "IX_FileTags_Source",
                table: "FileTags");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "FileTags");

            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "FileTags");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FileTags");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FileTags");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FileTags");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FileTags",
                table: "FileTags",
                columns: new[] { "FileId", "TagId" });
        }
    }
}
