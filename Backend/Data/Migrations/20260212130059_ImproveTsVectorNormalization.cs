using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ImproveTsVectorNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Files",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                oldStored: true);

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Directories",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Files",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                oldStored: true);

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Directories",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                oldStored: true);
        }
    }
}
