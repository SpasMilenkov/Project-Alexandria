using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueIndexPerCodecForRepresentations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StreamRepresentations_JobId_Codec",
                table: "StreamRepresentations");

            migrationBuilder.CreateIndex(
                name: "IX_StreamRepresentations_JobId_Codec",
                table: "StreamRepresentations",
                columns: new[] { "JobId", "Codec" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StreamRepresentations_JobId_Codec",
                table: "StreamRepresentations");

            migrationBuilder.CreateIndex(
                name: "IX_StreamRepresentations_JobId_Codec",
                table: "StreamRepresentations",
                columns: new[] { "JobId", "Codec" },
                unique: true);
        }
    }
}
