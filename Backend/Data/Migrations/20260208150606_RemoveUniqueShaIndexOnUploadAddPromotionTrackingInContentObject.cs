using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueShaIndexOnUploadAddPromotionTrackingInContentObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Uploads_Sha256",
                table: "Uploads");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPromotionAttemptAt",
                table: "ContentObjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromotedAt",
                table: "ContentObjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromotionAttempts",
                table: "ContentObjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_Sha256",
                table: "Uploads",
                column: "Sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Uploads_Sha256",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "LastPromotionAttemptAt",
                table: "ContentObjects");

            migrationBuilder.DropColumn(
                name: "PromotedAt",
                table: "ContentObjects");

            migrationBuilder.DropColumn(
                name: "PromotionAttempts",
                table: "ContentObjects");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_Sha256",
                table: "Uploads",
                column: "Sha256",
                unique: true);
        }
    }
}
