using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTsVectorToFilesAndDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_Name",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Directories_Name",
                table: "Directories");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Files",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Files",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Directories",
                type: "text",
                nullable: false,
                computedColumnSql: "regexp_replace(lower(coalesce(\"Name\", '')), '\\s+', ' ', 'g')",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Directories",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"Name\", ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_NormalizedName",
                table: "Files",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Directories_NormalizedName",
                table: "Directories",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_NormalizedName",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Directories_NormalizedName",
                table: "Directories");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Directories");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Directories");

            migrationBuilder.CreateIndex(
                name: "IX_Files_Name",
                table: "Files",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Directories_Name",
                table: "Directories",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }
    }
}
