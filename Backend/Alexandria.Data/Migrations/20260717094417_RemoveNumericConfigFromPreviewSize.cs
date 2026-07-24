using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNumericConfigFromPreviewSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Size",
                table: "Previews",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Size",
                table: "Previews",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
