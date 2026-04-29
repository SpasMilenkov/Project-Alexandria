using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileVerisonToFileRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Files",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple',\n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Files",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''),\n                        '\\.[^.]*$', ''\n                    ),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ),\n            '\\s+', ' ', 'g'\n        )",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                oldStored: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersions_FileId",
                table: "FileVersions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileVersions_Files_FileId",
                table: "FileVersions",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileVersions_Files_FileId",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_FileVersions_FileId",
                table: "FileVersions");

            migrationBuilder.DropIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Files",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', \n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple',\n                regexp_replace(\n                    regexp_replace(coalesce(\"Name\", ''), '\\.[^.]*$', ''),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            )",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Files",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''), \n                        '\\.[^.]*$', ''\n                    ), \n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ), \n            '\\s+', ' ', 'g'\n        )",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldComputedColumnSql: "regexp_replace(\n            lower(\n                regexp_replace(\n                    regexp_replace(\n                        coalesce(\"Name\", ''),\n                        '\\.[^.]*$', ''\n                    ),\n                    '[_\\-()[\\]]+', ' ', 'g'\n                )\n            ),\n            '\\s+', ' ', 'g'\n        )",
                oldStored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_CurrentVersionId",
                table: "Files",
                column: "CurrentVersionId",
                unique: true);
        }
    }
}
