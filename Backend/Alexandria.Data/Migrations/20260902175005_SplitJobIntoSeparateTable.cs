using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitJobIntoSeparateTable : Migration
    {
        /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_StreamRepresentations_TranspilationJobs_JobId",
            table: "StreamRepresentations");

        migrationBuilder.DropIndex(
            name: "IX_TranspilationJobs_Status",
            table: "TranspilationJobs");

        migrationBuilder.DropIndex(
            name: "IX_EssentiaBatchFiles_Status",
            table: "EssentiaBatchFiles");

        migrationBuilder.DropIndex(
            name: "IX_EssentiaBatchFiles_CompletedAt",
            table: "EssentiaBatchFiles");

        migrationBuilder.RenameColumn(
            name: "JobId",
            table: "StreamRepresentations",
            newName: "TranspilationId");

        migrationBuilder.RenameIndex(
            name: "IX_StreamRepresentations_JobId_Codec",
            table: "StreamRepresentations",
            newName: "IX_StreamRepresentations_TranspilationId_Codec");

        migrationBuilder.AddColumn<long>(
            name: "Size",
            table: "StreamRepresentations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "StorageQuota",
            table: "AspNetUsers",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<Guid>(
            name: "JobId",
            table: "TranspilationJobs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "JobId",
            table: "EssentiaBatchFiles",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                ProgressPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                ErrorDetail = table.Column<string>(type: "text", nullable: true),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Type = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_Jobs_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // --- Backfill: TranspilationJobs -> Jobs, reusing TranspilationJobs.Id ---
        // Type 0 = JobType.Transpilation. Status strings are identical between the
        // old TranspilationStatus and the new JobStatus, so a direct copy is exact.
        migrationBuilder.Sql(@"
            INSERT INTO ""Jobs"" (
                ""Id"", ""Status"", ""ProgressPercent"", ""RetryCount"", ""ErrorDetail"",
                ""StartedAt"", ""CompletedAt"", ""Type"", ""UserId"",
                ""CreatedAt"", ""UpdatedAt"", ""DeletedAt"", ""UpdatedBy"")
            SELECT
                tj.""Id"", tj.""Status"", tj.""ProgressPercent"", tj.""RetryCount"", tj.""ErrorDetail"",
                tj.""StartedAt"", tj.""CompletedAt"", 0, tj.""UserId"",
                tj.""CreatedAt"", tj.""UpdatedAt"", tj.""DeletedAt"", tj.""UpdatedBy""
            FROM ""TranspilationJobs"" tj;
        ");
        migrationBuilder.Sql(@"UPDATE ""TranspilationJobs"" SET ""JobId"" = ""Id"";");
        // --- End backfill ---

        // --- Backfill: EssentiaBatchFiles -> Jobs, reusing EssentiaBatchFiles.Id ---
        // Type 4 = JobType.MetadataEnrichment. Essentia has no JobStatus equivalent
        // for MissingOutput, so it collapses to Failed with a marker in ErrorDetail.
        // The system user (00000000-0000-0000-0000-000000000001) always exists.
        migrationBuilder.Sql(@"
            INSERT INTO ""Jobs"" (
                ""Id"", ""Status"", ""ProgressPercent"", ""RetryCount"", ""ErrorDetail"",
                ""StartedAt"", ""CompletedAt"", ""Type"", ""UserId"",
                ""CreatedAt"", ""UpdatedAt"", ""DeletedAt"", ""UpdatedBy"")
            SELECT
                ebf.""Id"",
                CASE ebf.""Status""
                    WHEN 'Pending'       THEN 'Queued'
                    WHEN 'Succeeded'     THEN 'Ready'
                    WHEN 'Failed'        THEN 'Failed'
                    WHEN 'MissingOutput' THEN 'Failed'
                END,
                CASE WHEN ebf.""Status"" = 'Succeeded' THEN 100 ELSE 0 END,
                0,
                CASE
                    WHEN ebf.""ErrorDetail"" IS NOT NULL THEN ebf.""ErrorDetail""
                    WHEN ebf.""Status"" = 'MissingOutput' THEN 'Missing output'
                    ELSE NULL
                END,
                eb.""DispatchedAt"",
                ebf.""CompletedAt"",
                4,
                '00000000-0000-0000-0000-000000000001',
                ebf.""CreatedAt"", ebf.""UpdatedAt"", ebf.""DeletedAt"", ebf.""UpdatedBy""
            FROM ""EssentiaBatchFiles"" ebf
            JOIN ""EssentiaBatches"" eb ON eb.""Id"" = ebf.""BatchId"";
        ");
        migrationBuilder.Sql(@"UPDATE ""EssentiaBatchFiles"" SET ""JobId"" = ""Id"";");
        // --- End backfill ---

        migrationBuilder.AlterColumn<Guid>(
            name: "JobId", table: "TranspilationJobs", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "JobId", table: "EssentiaBatchFiles", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TranspilationJobs_JobId", table: "TranspilationJobs",
            column: "JobId", unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EssentiaBatchFiles_JobId", table: "EssentiaBatchFiles", column: "JobId");

        migrationBuilder.CreateIndex(name: "IX_Jobs_CreatedAt", table: "Jobs", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_Jobs_Status", table: "Jobs", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_Jobs_UserId", table: "Jobs", column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_EssentiaBatchFiles_Jobs_JobId", table: "EssentiaBatchFiles",
            column: "JobId", principalTable: "Jobs", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_StreamRepresentations_TranspilationJobs_TranspilationId", table: "StreamRepresentations",
            column: "TranspilationId", principalTable: "TranspilationJobs", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_TranspilationJobs_Jobs_JobId", table: "TranspilationJobs",
            column: "JobId", principalTable: "Jobs", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropColumn(name: "CompletedAt", table: "TranspilationJobs");
        migrationBuilder.DropColumn(name: "ErrorDetail", table: "TranspilationJobs");
        migrationBuilder.DropColumn(name: "ProgressPercent", table: "TranspilationJobs");
        migrationBuilder.DropColumn(name: "RetryCount", table: "TranspilationJobs");
        migrationBuilder.DropColumn(name: "StartedAt", table: "TranspilationJobs");
        migrationBuilder.DropColumn(name: "Status", table: "TranspilationJobs");

        migrationBuilder.DropColumn(name: "CompletedAt", table: "EssentiaBatchFiles");
        migrationBuilder.DropColumn(name: "ErrorDetail", table: "EssentiaBatchFiles");
        migrationBuilder.DropColumn(name: "Status", table: "EssentiaBatchFiles");
    }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt", table: "TranspilationJobs",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ErrorDetail", table: "TranspilationJobs",
                type: "text", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent", table: "TranspilationJobs",
                type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "RetryCount", table: "TranspilationJobs",
                type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt", table: "TranspilationJobs",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Status", table: "TranspilationJobs",
                type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt", table: "EssentiaBatchFiles",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ErrorDetail", table: "EssentiaBatchFiles",
                type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Status", table: "EssentiaBatchFiles",
                type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "");

            // Copy the values back from Jobs before the table disappears.
            migrationBuilder.Sql(@"
                UPDATE ""TranspilationJobs"" tj
                SET ""Status"" = j.""Status"",
                    ""ProgressPercent"" = j.""ProgressPercent"",
                    ""RetryCount"" = j.""RetryCount"",
                    ""ErrorDetail"" = j.""ErrorDetail"",
                    ""StartedAt"" = j.""StartedAt"",
                    ""CompletedAt"" = j.""CompletedAt""
                FROM ""Jobs"" j
                WHERE tj.""JobId"" = j.""Id"";
            ");

            // Reverse the Essentia backfill. Lossy by design: MissingOutput collapsed
            // to Failed on the way up and cannot be recovered; in-flight Queued or
            // Processing jobs map back to Pending, Ready maps to Succeeded.
            migrationBuilder.Sql(@"
                UPDATE ""EssentiaBatchFiles"" ebf
                SET ""Status"" = CASE j.""Status""
                        WHEN 'Ready' THEN 'Succeeded'
                        WHEN 'Queued' THEN 'Pending'
                        WHEN 'Processing' THEN 'Pending'
                        ELSE 'Failed'
                    END,
                    ""ErrorDetail"" = j.""ErrorDetail"",
                    ""CompletedAt"" = j.""CompletedAt""
                FROM ""Jobs"" j
                WHERE ebf.""JobId"" = j.""Id"";
            ");

            migrationBuilder.DropForeignKey(name: "FK_StreamRepresentations_TranspilationJobs_TranspilationId", table: "StreamRepresentations");
            migrationBuilder.DropForeignKey(name: "FK_TranspilationJobs_Jobs_JobId", table: "TranspilationJobs");
            migrationBuilder.DropForeignKey(name: "FK_EssentiaBatchFiles_Jobs_JobId", table: "EssentiaBatchFiles");

            migrationBuilder.DropTable(name: "Jobs");

            migrationBuilder.DropIndex(name: "IX_TranspilationJobs_JobId", table: "TranspilationJobs");
            migrationBuilder.DropIndex(name: "IX_EssentiaBatchFiles_JobId", table: "EssentiaBatchFiles");

            migrationBuilder.DropColumn(name: "JobId", table: "TranspilationJobs");
            migrationBuilder.DropColumn(name: "Size", table: "StreamRepresentations");
            migrationBuilder.DropColumn(name: "JobId", table: "EssentiaBatchFiles");
            migrationBuilder.DropColumn(name: "StorageQuota", table: "AspNetUsers");

            migrationBuilder.RenameColumn(name: "TranspilationId", table: "StreamRepresentations", newName: "JobId");
            migrationBuilder.RenameIndex(name: "IX_StreamRepresentations_TranspilationId_Codec", table: "StreamRepresentations", newName: "IX_StreamRepresentations_JobId_Codec");

            migrationBuilder.CreateIndex(name: "IX_TranspilationJobs_Status", table: "TranspilationJobs", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_EssentiaBatchFiles_Status", table: "EssentiaBatchFiles", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_EssentiaBatchFiles_CompletedAt", table: "EssentiaBatchFiles", column: "CompletedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_StreamRepresentations_TranspilationJobs_JobId",
                table: "StreamRepresentations",
                column: "JobId",
                principalTable: "TranspilationJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}