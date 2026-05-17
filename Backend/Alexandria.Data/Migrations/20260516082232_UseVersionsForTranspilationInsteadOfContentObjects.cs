using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseVersionsForTranspilationInsteadOfContentObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("TRUNCATE TABLE \"TranspilationJobs\", \"StreamRepresentations\" CASCADE;");
            
            migrationBuilder.DropForeignKey(
                name: "FK_TranspilationJobs_ContentObjects_ContentObjectId",
                table: "TranspilationJobs");

            migrationBuilder.DropIndex(
                name: "IX_TranspilationJobs_ContentObjectId",
                table: "TranspilationJobs");

            migrationBuilder.DropColumn(
                name: "SegmentPrefix",
                table: "StreamRepresentations");

            migrationBuilder.RenameColumn(
                name: "ContentObjectId",
                table: "TranspilationJobs",
                newName: "VersionId");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorDetail",
                table: "TranspilationJobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentPrefix",
                table: "TranspilationJobs",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_VersionId_UserId",
                table: "TranspilationJobs",
                columns: new[] { "VersionId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TranspilationJobs_FileVersions_VersionId",
                table: "TranspilationJobs",
                column: "VersionId",
                principalTable: "FileVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranspilationJobs_FileVersions_VersionId",
                table: "TranspilationJobs");

            migrationBuilder.DropIndex(
                name: "IX_TranspilationJobs_VersionId_UserId",
                table: "TranspilationJobs");

            migrationBuilder.DropColumn(
                name: "SegmentPrefix",
                table: "TranspilationJobs");

            migrationBuilder.RenameColumn(
                name: "VersionId",
                table: "TranspilationJobs",
                newName: "ContentObjectId");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorDetail",
                table: "TranspilationJobs",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentPrefix",
                table: "StreamRepresentations",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranspilationJobs_ContentObjectId",
                table: "TranspilationJobs",
                column: "ContentObjectId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TranspilationJobs_ContentObjects_ContentObjectId",
                table: "TranspilationJobs",
                column: "ContentObjectId",
                principalTable: "ContentObjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
