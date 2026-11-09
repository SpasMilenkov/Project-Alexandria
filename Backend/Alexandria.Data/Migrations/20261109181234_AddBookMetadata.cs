using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDrmProtected",
                table: "DocumentMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Isbn",
                table: "DocumentMetadata",
                type: "varchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "DocumentMetadata",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Series",
                table: "DocumentMetadata",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SeriesIndex",
                table: "DocumentMetadata",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_Isbn",
                table: "DocumentMetadata",
                column: "Isbn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentMetadata_Isbn",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "IsDrmProtected",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "Isbn",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "Series",
                table: "DocumentMetadata");

            migrationBuilder.DropColumn(
                name: "SeriesIndex",
                table: "DocumentMetadata");
        }
    }
}
