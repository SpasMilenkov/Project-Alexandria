using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptureMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaptureMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CapturedOffsetMinutes = table.Column<short>(type: "smallint", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CameraMake = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CameraModel = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    LensModel = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Software = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    FocalLengthMm = table.Column<double>(type: "double precision", nullable: true),
                    FocalLength35mm = table.Column<int>(type: "integer", nullable: true),
                    FNumber = table.Column<double>(type: "double precision", nullable: true),
                    ExposureSeconds = table.Column<double>(type: "double precision", nullable: true),
                    Iso = table.Column<int>(type: "integer", nullable: true),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractorVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptureMetadata_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureMetadata_CapturedAt",
                table: "CaptureMetadata",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureMetadata_FileVersionId",
                table: "CaptureMetadata",
                column: "FileVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptureMetadata");
        }
    }
}
