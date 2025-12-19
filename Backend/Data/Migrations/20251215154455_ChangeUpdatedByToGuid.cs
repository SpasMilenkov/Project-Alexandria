using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUpdatedByToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Tags"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""SignedUrls"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""RefreshTokens"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Previews"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""MediaMetadata"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Files"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Directories"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""AspNetRoles"" ALTER COLUMN ""UpdatedBy"" TYPE uuid USING NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Tags"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""SignedUrls"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""RefreshTokens"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Previews"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""MediaMetadata"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Files"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""Directories"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""AspNetUsers"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(450) USING NULL;");

            migrationBuilder.Sql(@"ALTER TABLE ""AspNetRoles"" ALTER COLUMN ""UpdatedBy"" TYPE varchar(100) USING NULL;");
        }
    }
}
