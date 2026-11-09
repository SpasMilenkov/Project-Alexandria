using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alexandria.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GalleryDate",
                table: "Files",
                type: "timestamp with time zone",
                nullable: true);

            // One definition of the timeline date, shared by both triggers and the backfill.
            // Older versions count because editing a photo often strips its EXIF, and the edit
            // should stay where the original was taken. Versions newer than the current one are
            // ignored, so restoring version 1 also restores its date.
            // Without an offset, CapturedAt already holds the camera's wall clock; with one, the
            // offset turns the stored instant back into it.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION gallery_date_for_file(
                    p_mime_type          TEXT,
                    p_current_version_id UUID,
                    p_created_at         TIMESTAMPTZ)
                RETURNS TIMESTAMPTZ
                LANGUAGE sql
                STABLE
                AS $$
                    SELECT CASE
                        WHEN p_current_version_id IS NULL THEN NULL
                        WHEN p_mime_type NOT LIKE 'image/%' THEN NULL
                        WHEN p_mime_type IN ('image/svg+xml', 'image/x-icon', 'image/vnd.microsoft.icon') THEN NULL
                        WHEN EXISTS (
                            SELECT 1 FROM ""FileVersions""
                            WHERE ""Id"" = p_current_version_id AND ""IsEncrypted"") THEN NULL
                        ELSE COALESCE((
                            SELECT c.""CapturedAt"" + make_interval(mins => COALESCE(c.""CapturedOffsetMinutes"", 0))
                            FROM ""FileVersions"" cur
                            JOIN ""FileVersions"" v
                                ON v.""FileId"" = cur.""FileId"" AND v.""VersionNumber"" <= cur.""VersionNumber""
                            JOIN ""CaptureMetadata"" c ON c.""FileVersionId"" = v.""Id""
                            WHERE cur.""Id"" = p_current_version_id
                              AND c.""CapturedAt"" IS NOT NULL
                              AND c.""DeletedAt"" IS NULL
                            ORDER BY v.""VersionNumber"" DESC
                            LIMIT 1), p_created_at)
                    END
                $$;
            ");

            // Uploads, copies, restores and version switches all write CurrentVersionId through
            // different code paths, so the column is kept here instead of in each of them.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION files_set_gallery_date()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.""GalleryDate"" := gallery_date_for_file(NEW.""MimeType"", NEW.""CurrentVersionId"", NEW.""CreatedAt"");
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER files_gallery_date_trigger
                    BEFORE INSERT OR UPDATE OF ""CurrentVersionId"", ""MimeType"", ""CreatedAt"" ON ""Files""
                    FOR EACH ROW
                    EXECUTE FUNCTION files_set_gallery_date();
            ");

            // Metadata usually arrives after the upload, from the preview job or the backfill.
            // The distinct check skips writes when nothing moved, which is most backfill rows.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION capture_metadata_refresh_gallery_date()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_version_id UUID := COALESCE(NEW.""FileVersionId"", OLD.""FileVersionId"");
                BEGIN
                    UPDATE ""Files"" f
                    SET ""GalleryDate"" = gallery_date_for_file(f.""MimeType"", f.""CurrentVersionId"", f.""CreatedAt"")
                    FROM ""FileVersions"" v
                    WHERE v.""Id"" = v_version_id
                      AND f.""Id"" = v.""FileId""
                      AND f.""GalleryDate"" IS DISTINCT FROM
                          gallery_date_for_file(f.""MimeType"", f.""CurrentVersionId"", f.""CreatedAt"");

                    RETURN NULL;
                END;
                $$;

                CREATE TRIGGER capture_metadata_gallery_date_trigger
                    AFTER INSERT OR DELETE OR UPDATE OF ""CapturedAt"", ""CapturedOffsetMinutes"", ""DeletedAt""
                    ON ""CaptureMetadata""
                    FOR EACH ROW
                    EXECUTE FUNCTION capture_metadata_refresh_gallery_date();
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Files""
                SET ""GalleryDate"" = gallery_date_for_file(""MimeType"", ""CurrentVersionId"", ""CreatedAt"")
                WHERE ""MimeType"" LIKE 'image/%';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Files_Gallery_Taken",
                table: "Files",
                columns: new[] { "OwnerId", "GalleryDate", "Id" },
                filter: "\"GalleryDate\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Files_Gallery_Uploaded",
                table: "Files",
                columns: new[] { "OwnerId", "CreatedAt", "Id" },
                filter: "\"GalleryDate\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS capture_metadata_gallery_date_trigger ON ""CaptureMetadata"";
                DROP TRIGGER IF EXISTS files_gallery_date_trigger ON ""Files"";
                DROP FUNCTION IF EXISTS capture_metadata_refresh_gallery_date();
                DROP FUNCTION IF EXISTS files_set_gallery_date();
                DROP FUNCTION IF EXISTS gallery_date_for_file(TEXT, UUID, TIMESTAMPTZ);
            ");

            migrationBuilder.DropIndex(
                name: "IX_Files_Gallery_Taken",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_Gallery_Uploaded",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "GalleryDate",
                table: "Files");
        }
    }
}
