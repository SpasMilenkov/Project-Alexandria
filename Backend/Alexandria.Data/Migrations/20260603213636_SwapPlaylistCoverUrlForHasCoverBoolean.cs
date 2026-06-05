using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwapPlaylistCoverUrlForHasCoverBoolean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Playlists");

            migrationBuilder.AddColumn<bool>(
                name: "HasCover",
                table: "Playlists",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCover",
                table: "Playlists");

            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Playlists",
                type: "text",
                nullable: true);
        }
    }
}
