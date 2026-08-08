using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagExternalKeyAndFacet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                table: "Tags",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Facet",
                table: "Tags",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ExternalKey",
                table: "Tags",
                column: "ExternalKey",
                unique: true,
                filter: "\"ExternalKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_ParentId_Name",
                table: "Tags",
                columns: new[] { "OwnerId", "ParentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_ExternalKey",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_OwnerId_ParentId_Name",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "Facet",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }
    }
}
