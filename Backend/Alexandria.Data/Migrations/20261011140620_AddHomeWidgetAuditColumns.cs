using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeWidgetAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "HomeWidgets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "HomeWidgets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "HomeWidgets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "HomeWidgets",
                type: "uuid",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "HomeWidgets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "HomeWidgets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "HomeWidgets");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "HomeWidgets");
        }
    }
}
