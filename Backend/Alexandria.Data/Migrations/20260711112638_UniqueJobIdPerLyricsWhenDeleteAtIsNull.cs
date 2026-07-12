using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueJobIdPerLyricsWhenDeleteAtIsNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_TranspilationJobId",
                table: "TrackLyrics");

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_TranspilationJobId",
                table: "TrackLyrics",
                column: "TranspilationJobId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_TranspilationJobId",
                table: "TrackLyrics");

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_TranspilationJobId",
                table: "TrackLyrics",
                column: "TranspilationJobId",
                unique: true);
        }
    }
}
