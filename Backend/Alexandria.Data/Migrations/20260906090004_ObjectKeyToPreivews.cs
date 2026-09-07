using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class ObjectKeyToPreivews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "Previews",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            // Backfill keys for rows written before key tracking. All writers use
            // "previews/{hex}" for Preview kind and "thumbnails/{hex}" for Thumbnail
            // kind, where hex is the lowercase hex of the version content hash.
            migrationBuilder.Sql(@"UPDATE ""Previews"" p SET ""ObjectKey"" =
                CASE WHEN p.""Kind"" = 'Thumbnail'
                    THEN 'thumbnails/' || encode(v.""ContentHash"", 'hex')
                    ELSE 'previews/' || encode(v.""ContentHash"", 'hex') END
                FROM ""FileVersions"" v
                WHERE v.""Id"" = p.""VersionId"" AND p.""ObjectKey"" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "Previews");
        }
    }
}
