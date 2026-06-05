using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserIdToTranspilationJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "TranspilationJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_UserId",
                table: "TranspilationJobs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TranspilationJobs_AspNetUsers_UserId",
                table: "TranspilationJobs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranspilationJobs_AspNetUsers_UserId",
                table: "TranspilationJobs");

            migrationBuilder.DropIndex(
                name: "IX_TranspilationJobs_UserId",
                table: "TranspilationJobs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TranspilationJobs");
        }
    }
}
