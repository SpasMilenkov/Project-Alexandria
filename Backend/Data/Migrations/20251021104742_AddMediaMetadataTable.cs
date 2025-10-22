using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaMetadataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Duration = table.Column<double>(type: "double precision", nullable: false),
                    BitrateMbps = table.Column<double>(type: "double precision", nullable: false),
                    FormatName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    VideoCodec = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    AudioCodec = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    HasAudio = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Artist = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Album = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Year = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Genre = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaMetadata_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_CreatedAt",
                table: "MediaMetadata",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_FileId",
                table: "MediaMetadata",
                column: "FileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaMetadata");
        }
    }
}
