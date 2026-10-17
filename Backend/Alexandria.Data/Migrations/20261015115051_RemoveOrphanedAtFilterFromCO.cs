using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrphanedAtFilterFromCO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // E01 dedup: the old filtered index permitted one active plus any
            // number of orphaned rows sharing a hash, all pointing at the same
            // content/{hash} object. Merge those aliases onto a single survivor
            // before the stricter index lands, or CreateIndex fails.
            // Survivor mirrors HashExistsAsync ordering: promoted first, then
            // oldest. Only FileVersions references ContentObjects, so repointing
            // it preserves every version while the loser rows hard-delete.
            // Idempotent: both statements no-op when no duplicates exist.
            migrationBuilder.Sql(@"
                UPDATE ""FileVersions"" AS fv
                SET ""ContentObjectId"" = s.""SurvivorId""
                FROM (
                    SELECT ""Id"",
                           FIRST_VALUE(""Id"") OVER (
                               PARTITION BY ""Hash""
                               ORDER BY ""IsPromoted"" DESC, ""CreatedAt"" ASC, ""Id"" ASC
                           ) AS ""SurvivorId""
                    FROM ""ContentObjects""
                    WHERE ""DeletedAt"" IS NULL
                ) AS s
                WHERE fv.""ContentObjectId"" = s.""Id""
                  AND fv.""ContentObjectId"" <> s.""SurvivorId"";
            ");

            migrationBuilder.Sql(@"
                DELETE FROM ""ContentObjects"" AS co
                USING (
                    SELECT ""Id"",
                           FIRST_VALUE(""Id"") OVER (
                               PARTITION BY ""Hash""
                               ORDER BY ""IsPromoted"" DESC, ""CreatedAt"" ASC, ""Id"" ASC
                           ) AS ""SurvivorId""
                    FROM ""ContentObjects""
                    WHERE ""DeletedAt"" IS NULL
                ) AS s
                WHERE co.""Id"" = s.""Id""
                  AND co.""Id"" <> s.""SurvivorId"";
            ");

            migrationBuilder.DropIndex(
                name: "ix_contentobjects_hash_active",
                table: "ContentObjects");

            migrationBuilder.CreateIndex(
                name: "ix_contentobjects_not_deleted",
                table: "ContentObjects",
                column: "Hash",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contentobjects_not_deleted",
                table: "ContentObjects");

            migrationBuilder.CreateIndex(
                name: "ix_contentobjects_hash_active",
                table: "ContentObjects",
                column: "Hash",
                unique: true,
                filter: "\"OrphanedAt\" IS NULL");
        }
    }
}
