using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsInstrumentalColumnToTrackLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ConfidenceScore",
                table: "TrackLyrics",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<bool>(
                name: "IsInstrumental",
                table: "TrackLyrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_Id",
                table: "TrackLyrics",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TrackLyrics_Status",
                table: "TrackLyrics",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_Id",
                table: "TrackLyrics");

            migrationBuilder.DropIndex(
                name: "IX_TrackLyrics_Status",
                table: "TrackLyrics");

            migrationBuilder.DropColumn(
                name: "IsInstrumental",
                table: "TrackLyrics");

            migrationBuilder.AlterColumn<decimal>(
                name: "ConfidenceScore",
                table: "TrackLyrics",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
