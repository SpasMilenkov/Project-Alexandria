using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSignedUrlCreatorIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 ALTER TABLE "SignedUrls"
                                 ALTER COLUMN "CreatorId" TYPE uuid
                                 USING "CreatorId"::uuid;
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 ALTER TABLE "SignedUrls"
                                 ALTER COLUMN "CreatorId" TYPE varchar(450)
                                 USING "CreatorId"::text;
                                 """);
        }
    }
}
