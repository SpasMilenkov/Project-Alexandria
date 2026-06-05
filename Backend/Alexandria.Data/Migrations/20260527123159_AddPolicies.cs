using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true),
                    DirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    InheritedByChildren = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectoryPolicies_Directories_DirectoryId",
                        column: x => x.DirectoryId,
                        principalTable: "Directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TriggerType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TriggerValue = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    ApplyOnNewVersion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyRules_DirectoryPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "DirectoryPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryPolicies_DeletedAt",
                table: "DirectoryPolicies",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryPolicies_DirectoryId",
                table: "DirectoryPolicies",
                column: "DirectoryId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyRules_PolicyId_Priority",
                table: "PolicyRules",
                columns: new[] { "PolicyId", "Priority" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyRules_TriggerValue",
                table: "PolicyRules",
                column: "TriggerValue",
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyRules");

            migrationBuilder.DropTable(
                name: "DirectoryPolicies");
        }
    }
}
