using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Rotated tokens of one sign-in share the user and start time, so each such group gets
            // one id. Left at the default, every existing session would share it and revoking one
            // would end them all.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" AS rt
                SET "SessionId" = s.session_id
                FROM (
                    SELECT "UserId", "SessionStartedAt", gen_random_uuid() AS session_id
                    FROM "RefreshTokens"
                    GROUP BY "UserId", "SessionStartedAt"
                ) AS s
                WHERE rt."UserId" = s."UserId"
                  AND rt."SessionStartedAt" = s."SessionStartedAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");
        }
    }
}
