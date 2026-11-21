using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorySystemKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SystemKind",
                table: "Directories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Directories_Owner_SystemKind",
                table: "Directories",
                columns: new[] { "OwnerId", "SystemKind" },
                unique: true,
                filter: "\"SystemKind\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Directories_Owner_SystemKind",
                table: "Directories");

            migrationBuilder.DropColumn(
                name: "SystemKind",
                table: "Directories");
        }
    }
}
