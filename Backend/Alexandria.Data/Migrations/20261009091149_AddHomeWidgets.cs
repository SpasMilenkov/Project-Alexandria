using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeWidgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Layout = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ShowTitle = table.Column<bool>(type: "boolean", nullable: true),
                    OptionsVersion = table.Column<int>(type: "integer", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeWidgets", x => x.Id);
                    table.CheckConstraint("CK_HomeWidgets_HorizontalBounds", "\"Width\" >= 1 AND \"X\" >= 0 AND\n((\"Layout\" = 0 AND \"Width\" <= 12 AND \"X\" <= 12 - \"Width\") OR\n (\"Layout\" = 1 AND \"Width\" <= 4 AND \"X\" <= 4 - \"Width\"))");
                    table.CheckConstraint("CK_HomeWidgets_Layout", "\"Layout\" IN (0, 1)");
                    table.CheckConstraint("CK_HomeWidgets_OptionsObject", "jsonb_typeof(\"Options\") = 'object'");
                    table.CheckConstraint("CK_HomeWidgets_OptionsVersion", "\"OptionsVersion\" >= 1");
                    table.CheckConstraint("CK_HomeWidgets_VerticalBounds", "\"Height\" BETWEEN 1 AND 12 AND \"Y\" >= 0 AND \"Y\" <= 256 - \"Height\"");
                    table.ForeignKey(
                        name: "FK_HomeWidgets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeWidgets_UserId_Layout",
                table: "HomeWidgets",
                columns: new[] { "UserId", "Layout" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeWidgets");
        }
    }
}
