using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateQualifiedPlaysFromFinishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReachedCompletionThreshold",
                table: "StreamSession",
                newName: "IsQualifiedPlay");

            migrationBuilder.RenameIndex(
                name: "IX_StreamSession_ReachedCompletionThreshold",
                table: "StreamSession",
                newName: "IX_StreamSession_IsQualifiedPlay");

            migrationBuilder.RenameColumn(
                name: "TimesCompleted",
                table: "StreamHistory",
                newName: "QualifiedPlayCount");

            migrationBuilder.RenameColumn(
                name: "LastCompletedAt",
                table: "StreamHistory",
                newName: "LastPlayedAt");

            migrationBuilder.AddColumn<bool>(
                name: "PlaybackFinished",
                table: "StreamSession",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasFinished",
                table: "StreamHistory",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaybackFinished",
                table: "StreamSession");

            migrationBuilder.DropColumn(
                name: "HasFinished",
                table: "StreamHistory");

            migrationBuilder.RenameColumn(
                name: "IsQualifiedPlay",
                table: "StreamSession",
                newName: "ReachedCompletionThreshold");

            migrationBuilder.RenameColumn(
                name: "QualifiedPlayCount",
                table: "StreamHistory",
                newName: "TimesCompleted");

            migrationBuilder.RenameColumn(
                name: "LastPlayedAt",
                table: "StreamHistory",
                newName: "LastCompletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_StreamSession_IsQualifiedPlay",
                table: "StreamSession",
                newName: "IX_StreamSession_ReachedCompletionThreshold");
        }
    }
}
