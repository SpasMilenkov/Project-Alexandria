using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDefaultStorageQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows predate quota tracking (0 = unlimited). Give real accounts the
            // 10 GB default; the system service account keeps 0 (unlimited).
            migrationBuilder.Sql(@"UPDATE ""AspNetUsers"" SET ""StorageQuota"" = 10737418240
                WHERE ""StorageQuota"" = 0 AND ""Id"" <> '00000000-0000-0000-0000-000000000001';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Previous per-user values are unrecoverable; intentional no-op.
        }
    }
}
