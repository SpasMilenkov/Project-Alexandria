using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Keywords = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Language = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Company = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Application = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Producer = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    AuthoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthoredOffsetMinutes = table.Column<short>(type: "smallint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedOffsetMinutes = table.Column<short>(type: "smallint", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: true),
                    SlideCount = table.Column<int>(type: "integer", nullable: true),
                    SheetCount = table.Column<int>(type: "integer", nullable: true),
                    EditingMinutes = table.Column<int>(type: "integer", nullable: true),
                    PaperSize = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    IsPasswordProtected = table.Column<bool>(type: "boolean", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractorVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentMetadata_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_AuthoredAt",
                table: "DocumentMetadata",
                column: "AuthoredAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_FileVersionId",
                table: "DocumentMetadata",
                column: "FileVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentMetadata");
        }
    }
}
