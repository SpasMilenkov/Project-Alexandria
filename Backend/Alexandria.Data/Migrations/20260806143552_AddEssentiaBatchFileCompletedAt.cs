using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEssentiaBatchFileCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "EssentiaBatchFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatchFiles_CompletedAt",
                table: "EssentiaBatchFiles",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatchFiles_CreatedAt",
                table: "EssentiaBatchFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EssentiaBatchFiles_Status",
                table: "EssentiaBatchFiles",
                column: "Status");
            
            migrationBuilder.Sql(@"UPDATE ""EssentiaBatchFiles"" SET ""CompletedAt"" = ""UpdatedAt"" WHERE ""Status"" IN ('Succeeded', 'Failed', 'MissingOutput')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EssentiaBatchFiles_CompletedAt",
                table: "EssentiaBatchFiles");

            migrationBuilder.DropIndex(
                name: "IX_EssentiaBatchFiles_CreatedAt",
                table: "EssentiaBatchFiles");

            migrationBuilder.DropIndex(
                name: "IX_EssentiaBatchFiles_Status",
                table: "EssentiaBatchFiles");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "EssentiaBatchFiles");
        }
    }
}
