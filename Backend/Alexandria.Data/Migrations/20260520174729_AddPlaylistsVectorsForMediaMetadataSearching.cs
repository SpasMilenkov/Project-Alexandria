using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistsVectorsForMediaMetadataSearching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedSearch",
                table: "MediaMetadata",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(\n            coalesce(\"Title\", '') || ' ' ||\n            coalesce(\"Artist\", '') || ' ' ||\n            coalesce(\"Album\", '') || ' ' ||\n            coalesce(\"Genre\", '')\n        )",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "MediaMetadata",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "setweight(to_tsvector('simple', coalesce(\"Title\", '')),  'A') ||\n          setweight(to_tsvector('simple', coalesce(\"Artist\", '')), 'B') ||\n          setweight(to_tsvector('simple', coalesce(\"Album\", '')),  'C') ||\n          setweight(to_tsvector('simple', coalesce(\"Genre\", '')),  'D')",
                stored: true);

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverUrl = table.Column<string>(type: "text", nullable: true),
                    AmbientTheme = table.Column<string>(type: "text", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    TranspilationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_TranspilationJobs_TranspilationJobId",
                        column: x => x.TranspilationJobId,
                        principalTable: "TranspilationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_NormalizedSearch",
                table: "MediaMetadata",
                column: "NormalizedSearch")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadata_SearchVector",
                table: "MediaMetadata",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId",
                table: "PlaylistItems",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TranspilationJobId",
                table: "PlaylistItems",
                column: "TranspilationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_OwnerId",
                table: "Playlists",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_MediaMetadata_NormalizedSearch",
                table: "MediaMetadata");

            migrationBuilder.DropIndex(
                name: "IX_MediaMetadata_SearchVector",
                table: "MediaMetadata");

            migrationBuilder.DropColumn(
                name: "NormalizedSearch",
                table: "MediaMetadata");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "MediaMetadata");
        }
    }
}
