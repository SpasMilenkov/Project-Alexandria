using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultUserId = new Guid("019a3f05-659b-7628-9102-1eef78035977");

            // Add OwnerId column to Tags (nullable first)
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Tags",
                type: "uuid",
                nullable: true);

            // Update existing Tags to use default user
            migrationBuilder.Sql($@"
                UPDATE ""Tags"" 
                SET ""OwnerId"" = '{defaultUserId}' 
                WHERE ""OwnerId"" IS NULL;
            ");

            // Make Tags.OwnerId non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Tags",
                type: "uuid",
                nullable: false);

            // Add DirectoryId to Files (nullable, no default needed)
            migrationBuilder.AddColumn<Guid>(
                name: "DirectoryId",
                table: "Files",
                type: "uuid",
                nullable: true);

            // Add OwnerId column to Files (nullable first)
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Files",
                type: "uuid",
                nullable: true);

            // Update existing Files to use default user
            migrationBuilder.Sql($@"
                UPDATE ""Files"" 
                SET ""OwnerId"" = '{defaultUserId}' 
                WHERE ""OwnerId"" IS NULL;
            ");

            // Make Files.OwnerId non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Files",
                type: "uuid",
                nullable: false);

            // Create Directories table
            migrationBuilder.CreateTable(
                name: "Directories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Directories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Directories_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Directories_Directories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId",
                table: "Tags",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_DirectoryId",
                table: "Files",
                column: "DirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerId",
                table: "Files",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Directories_OwnerId",
                table: "Directories",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Directories_ParentId_Name",
                table: "Directories",
                columns: new[] { "ParentId", "Name" },
                unique: true);

            // Add foreign key constraints AFTER data is populated
            migrationBuilder.AddForeignKey(
                name: "FK_Files_AspNetUsers_OwnerId",
                table: "Files",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Directories_DirectoryId",
                table: "Files",
                column: "DirectoryId",
                principalTable: "Directories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_AspNetUsers_OwnerId",
                table: "Tags",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_AspNetUsers_OwnerId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Directories_DirectoryId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_AspNetUsers_OwnerId",
                table: "Tags");

            migrationBuilder.DropTable(
                name: "Directories");

            migrationBuilder.DropIndex(
                name: "IX_Tags_OwnerId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Files_DirectoryId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_OwnerId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "DirectoryId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Files");
        }
    }
}
