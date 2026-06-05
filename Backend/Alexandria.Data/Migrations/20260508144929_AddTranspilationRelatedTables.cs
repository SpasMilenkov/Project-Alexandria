using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranspilationRelatedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreamHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionSeconds = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamHistory_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamHistory_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranspilationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorDetail = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranspilationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranspilationJobs_ContentObjects_ContentObjectId",
                        column: x => x.ContentObjectId,
                        principalTable: "ContentObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamRepresentations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codec = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    BitrateKbps = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    SegmentPrefix = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRepresentations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRepresentations_TranspilationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TranspilationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamHistory_FileId",
                table: "StreamHistory",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamHistory_LastAccessedAt",
                table: "StreamHistory",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StreamHistory_UserId_FileId",
                table: "StreamHistory",
                columns: new[] { "UserId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRepresentations_JobId_Codec",
                table: "StreamRepresentations",
                columns: new[] { "JobId", "Codec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRepresentations_Status",
                table: "StreamRepresentations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_ContentObjectId",
                table: "TranspilationJobs",
                column: "ContentObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_CreatedAt",
                table: "TranspilationJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_Status",
                table: "TranspilationJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamHistory");

            migrationBuilder.DropTable(
                name: "StreamRepresentations");

            migrationBuilder.DropTable(
                name: "TranspilationJobs");
        }
    }
}
