using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackLyricsSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "TrackLyrics",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\n            nullif(btrim(\"PlainLyrics\"), ''),\n            regexp_replace(coalesce(\"SyncedLyrics\", ''), '\\[[^\\]]*\\]', ' ', 'g')\n        ))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_SearchVector",
                table: "TrackLyrics",
                column: "SearchVector",
                filter: "\"DeletedAt\" IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_SearchVector",
                table: "TrackLyrics");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "TrackLyrics");
        }
    }
}
