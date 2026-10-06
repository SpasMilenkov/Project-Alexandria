using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOverviewSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OverviewSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverviewSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OverviewSummaries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OverviewSummaries_UserId_Kind_PeriodStart_PeriodEnd",
                table: "OverviewSummaries",
                columns: new[] { "UserId", "Kind", "PeriodStart", "PeriodEnd" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OverviewSummaries");
        }
    }
}
