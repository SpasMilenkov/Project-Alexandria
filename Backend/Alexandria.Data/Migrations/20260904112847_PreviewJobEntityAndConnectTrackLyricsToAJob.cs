using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreviewJobEntityAndConnectTrackLyricsToAJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "TrackLyrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreviewJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviewJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreviewJobs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreviewJobs_FileVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreviewJobs_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreviewJobs_CreatedAt",
                table: "PreviewJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PreviewJobs_JobId",
                table: "PreviewJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreviewJobs_UserId",
                table: "PreviewJobs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PreviewJobs_VersionId_Kind_UserId",
                table: "PreviewJobs",
                columns: new[] { "VersionId", "Kind", "UserId" },
                unique: true);

            // --- Backfill: TrackLyrics -> Jobs, reusing TrackLyrics.Id ---
            // Type 3 = JobType.LyricsFetch. LyricsStatus is stored as int
            // (PendingFetch=0, Fetching=1, Fetched=2, FetchFailed=3, NoMatch=4).
            // Fetched and NoMatch are both successful terminal outcomes (Ready).
            // Fetch-failure detail was never persisted on the lyrics row, so
            // ErrorDetail starts NULL; StartedAt is unknown historically.
            migrationBuilder.Sql(@"
                INSERT INTO ""Jobs"" (
                    ""Id"", ""Status"", ""ProgressPercent"", ""RetryCount"", ""ErrorDetail"",
                    ""StartedAt"", ""CompletedAt"", ""Type"", ""UserId"",
                    ""CreatedAt"", ""UpdatedAt"", ""DeletedAt"", ""UpdatedBy"")
                SELECT
                    tl.""Id"",
                    CASE tl.""Status""
                        WHEN 0 THEN 'Queued'
                        WHEN 1 THEN 'Processing'
                        WHEN 2 THEN 'Ready'
                        WHEN 4 THEN 'Ready'
                        WHEN 3 THEN 'Failed'
                    END,
                    CASE WHEN tl.""Status"" IN (2, 4) THEN 100 ELSE 0 END,
                    0,
                    NULL,
                    NULL,
                    tl.""FetchedAt"",
                    3,
                    tj.""UserId"",
                    tl.""CreatedAt"", tl.""UpdatedAt"", tl.""DeletedAt"", tl.""UpdatedBy""
                FROM ""TrackLyrics"" tl
                JOIN ""TranspilationJobs"" tj ON tj.""Id"" = tl.""TranspilationJobId"";
            ");
            migrationBuilder.Sql(@"UPDATE ""TrackLyrics"" SET ""JobId"" = ""Id"";");
            // --- End backfill ---

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId", table: "TrackLyrics", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_JobId",
                table: "TrackLyrics",
                column: "JobId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackLyrics_Jobs_JobId",
                table: "TrackLyrics",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackLyrics_Jobs_JobId",
                table: "TrackLyrics");

            migrationBuilder.DropTable(
                name: "PreviewJobs");

            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_JobId",
                table: "TrackLyrics");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "TrackLyrics");
        }
    }
}
