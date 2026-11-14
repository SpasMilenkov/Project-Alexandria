using Alexandria.Data.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorySize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DirectFilesSize",
                table: "Directories",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // The refresh function, the Files triggers that call it, the read-side subtree sum and
            // the backfill of existing folders. The scripts live in Sql/DirectorySize/AddDirectorySize
            // and run in name order.
            foreach (var script in EmbeddedSql.ReadFolder("DirectorySize.AddDirectorySize"))
                migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS files_directory_size_insert_trigger ON ""Files"";
                DROP TRIGGER IF EXISTS files_directory_size_update_trigger ON ""Files"";
                DROP TRIGGER IF EXISTS files_directory_size_delete_trigger ON ""Files"";
                DROP FUNCTION IF EXISTS files_directory_size_after_insert();
                DROP FUNCTION IF EXISTS files_directory_size_after_update();
                DROP FUNCTION IF EXISTS files_directory_size_after_delete();
                DROP FUNCTION IF EXISTS refresh_directory_direct_files_size(UUID[]);
                DROP FUNCTION IF EXISTS directory_tree_size(UUID);
            ");

            migrationBuilder.DropColumn(
                name: "DirectFilesSize",
                table: "Directories");
        }
    }
}
