using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completed",
                table: "StreamHistory");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCompletedAt",
                table: "StreamHistory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxPositionReachedSeconds",
                table: "StreamHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "TimesCompleted",
                table: "StreamHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalListenedSeconds",
                table: "StreamHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "StreamSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartPositionSeconds = table.Column<long>(type: "bigint", nullable: false),
                    EndPositionSeconds = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ListenedSeconds = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ReachedCompletionThreshold = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamSession_StreamHistory_StreamHistoryId",
                        column: x => x.StreamHistoryId,
                        principalTable: "StreamHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamSession_ReachedCompletionThreshold",
                table: "StreamSession",
                column: "ReachedCompletionThreshold");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSession_StreamHistoryId",
                table: "StreamSession",
                column: "StreamHistoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamSession");

            migrationBuilder.DropColumn(
                name: "LastCompletedAt",
                table: "StreamHistory");

            migrationBuilder.DropColumn(
                name: "MaxPositionReachedSeconds",
                table: "StreamHistory");

            migrationBuilder.DropColumn(
                name: "TimesCompleted",
                table: "StreamHistory");

            migrationBuilder.DropColumn(
                name: "TotalListenedSeconds",
                table: "StreamHistory");

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "StreamHistory",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
