using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEssentiaEnrichmentBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileEnrichments_FileId",
                table: "FileEnrichments");

            migrationBuilder.CreateTable(
                name: "EssentiaBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Backbone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EssentiaBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EssentiaBatchFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ErrorDetail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EssentiaBatchFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EssentiaBatchFiles_EssentiaBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "EssentiaBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EssentiaBatchFiles_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileEnrichments_FileId_Analyzer_Version",
                table: "FileEnrichments",
                columns: new[] { "FileId", "Analyzer", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatches_Backbone",
                table: "EssentiaBatches",
                column: "Backbone");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatches_CreatedAt",
                table: "EssentiaBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatches_Status",
                table: "EssentiaBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatchFiles_BatchId",
                table: "EssentiaBatchFiles",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatchFiles_FileId",
                table: "EssentiaBatchFiles",
                column: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EssentiaBatchFiles");

            migrationBuilder.DropTable(
                name: "EssentiaBatches");

            migrationBuilder.DropIndex(
                name: "IX_FileEnrichments_FileId_Analyzer_Version",
                table: "FileEnrichments");

            migrationBuilder.CreateIndex(
                name: "IX_FileEnrichments_FileId",
                table: "FileEnrichments",
                column: "FileId");
        }
    }
}
