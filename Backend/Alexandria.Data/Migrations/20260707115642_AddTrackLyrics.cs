using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LyricsId",
                table: "TranspilationJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrackLyrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlainLyrics = table.Column<string>(type: "text", nullable: true),
                    SyncedLyrics = table.Column<string>(type: "text", nullable: true),
                    SourceProvider = table.Column<int>(type: "integer", nullable: false),
                    Cached = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderTrackId = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TranspilationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackLyrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackLyrics_TranspilationJobs_TranspilationJobId",
                        column: x => x.TranspilationJobId,
                        principalTable: "TranspilationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_TranspilationJobId",
                table: "TrackLyrics",
                column: "TranspilationJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackLyrics");

            migrationBuilder.DropColumn(
                name: "LyricsId",
                table: "TranspilationJobs");
        }
    }
}
