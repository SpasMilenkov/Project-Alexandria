using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalEventLastSeenAndActiveUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "OperationalEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "OperationalEvents" SET "LastSeenAt" = COALESCE("UpdatedAt", "CreatedAt");
                """);

            // The unique index cannot be created while producers' races have left duplicate
            // active incidents behind; keep the oldest of each group open.
            migrationBuilder.Sql("""
                UPDATE "OperationalEvents" AS e
                SET "Status" = 'Resolved', "ResolvedAt" = NOW(), "UpdatedAt" = NOW()
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "ServiceType", "Code" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "OperationalEvents"
                    WHERE "Status" = 'Active' AND "Code" <> 'UserReportedProblem'
                ) AS d
                WHERE e."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_ServiceType_Code_ActiveUnique",
                table: "OperationalEvents",
                columns: new[] { "ServiceType", "Code" },
                unique: true,
                filter: "\"Status\" = 'Active' AND \"Code\" <> 'UserReportedProblem'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationalEvents_ServiceType_Code_ActiveUnique",
                table: "OperationalEvents");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "OperationalEvents");
        }
    }
}
