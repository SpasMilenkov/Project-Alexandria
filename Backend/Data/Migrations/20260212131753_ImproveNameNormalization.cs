using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ImproveNameNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Files",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Directories",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Files",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Directories",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                oldStored: true);
        }
    }
}
