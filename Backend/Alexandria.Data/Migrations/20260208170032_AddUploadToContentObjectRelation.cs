using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadToContentObjectRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadId",
                table: "ContentObjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentObjects_UploadId",
                table: "ContentObjects",
                column: "UploadId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentObjects_Uploads_UploadId",
                table: "ContentObjects",
                column: "UploadId",
                principalTable: "Uploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentObjects_Uploads_UploadId",
                table: "ContentObjects");

            migrationBuilder.DropIndex(
                name: "IX_ContentObjects_UploadId",
                table: "ContentObjects");

            migrationBuilder.DropColumn(
                name: "UploadId",
                table: "ContentObjects");
        }
    }
}
