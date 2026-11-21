using Alexandria.Data.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTextSearchFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The index-only lookup the file search uses for document text. The script lives in
            // Sql/DocumentText/AddDocumentTextSearchFunction.
            foreach (var script in EmbeddedSql.ReadFolder("DocumentText.AddDocumentTextSearchFunction"))
                migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS matching_document_files(UUID, TEXT);");
        }
    }
}
