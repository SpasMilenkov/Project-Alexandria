using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentText",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    IsTruncated = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorDetail = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractorVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentText", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentText_ContentObjects_ContentObjectId",
                        column: x => x.ContentObjectId,
                        principalTable: "ContentObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentText_ContentObjectId",
                table: "DocumentText",
                column: "ContentObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentText_SearchVector",
                table: "DocumentText",
                column: "SearchVector",
                filter: "\"DeletedAt\" IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentText_Status",
                table: "DocumentText",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentText");
        }
    }
}
