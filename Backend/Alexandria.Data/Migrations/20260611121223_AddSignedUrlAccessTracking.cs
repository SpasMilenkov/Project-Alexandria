using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignedUrlAccessTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessCount",
                table: "SignedUrls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccessedAt",
                table: "SignedUrls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAccessCount",
                table: "SignedUrls",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessCount",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "LastAccessedAt",
                table: "SignedUrls");

            migrationBuilder.DropColumn(
                name: "MaxAccessCount",
                table: "SignedUrls");
        }
    }
}
