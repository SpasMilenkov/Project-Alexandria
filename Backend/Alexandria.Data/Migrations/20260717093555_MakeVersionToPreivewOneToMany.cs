using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeVersionToPreivewOneToMany : Migration
    {
        //THIS IS NOT NEEDED, IT WAS APPARENTLY REMOVING SOMETHING I DID ON DEV
        //COMMENTING THIS OUT ONLY BECAUSE I DON'T WANT TO WIPE ALL THE DATA
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropIndex(
            //     name: "IX_Previews_VersionId",
            //     table: "Previews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.CreateIndex(
            //     name: "IX_Previews_VersionId",
            //     table: "Previews",
            //     column: "VersionId",
            //     unique: true);
        }
    }
}
