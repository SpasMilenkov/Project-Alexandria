using System.Text;
using AwesomeAssertions;

namespace Builder.Tests.Integration;

public sealed class WorkerProcessingTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public void Processing_real_workers_preserve_core_metadata_and_complete_previews_streaming_and_playlist_sync()
    {
        using var fixture = new CachedWorkerProcessingFixture();
        fixture.PrepareAndStart();

        var audio = fixture.SeedFile("metadata-audio.wav", "audio/wav", CreateWave());
        var metadataJob = fixture.QueuePreview(audio.VersionId, "media.wav");
        fixture.WaitForJob(metadataJob);
        fixture.Sql($"SELECT \"Artist\" FROM \"MediaMetadata\" WHERE \"FileId\"='{audio.FileId}';")
            .Should().Be("Fixture Artist");
        fixture.Sql($"SELECT COUNT(*) FROM \"Previews\" WHERE \"VersionId\"='{audio.VersionId}';")
            .Should().Be("0");
        fixture.Sql("SELECT COUNT(*) FROM \"EssentiaBatches\";").Should().Be("0");

        fixture.EnableMediaPreviews();
        var mediaJob = fixture.QueuePreview(audio.VersionId, "media.wav");
        fixture.WaitForJob(mediaJob);
        fixture.Sql($"SELECT COUNT(*) FROM \"Previews\" WHERE \"VersionId\"='{audio.VersionId}';")
            .Should().Be("2");
        fixture.VerifyStoredOutputs("alexandria-previews", "previews/");
        fixture.VerifyStoredOutputs("alexandria-previews", "thumbnails/");

        var document = fixture.SeedFile("preview-document.pdf", "application/pdf", CreatePdf());
        var documentJob = fixture.QueuePreview(document.VersionId, "document.pdf", document: true);
        fixture.WaitForJob(documentJob);
        fixture.Sql($"SELECT COUNT(*) FROM \"Previews\" WHERE \"VersionId\"='{document.VersionId}';")
            .Should().Be("2");

        var transcodeJob = fixture.QueueTranspilation(audio.VersionId);
        fixture.WaitForJob(transcodeJob);
        fixture.Sql($"SELECT COUNT(*) FROM \"StreamRepresentations\" r JOIN \"TranspilationJobs\" t ON t.\"Id\"=r.\"TranspilationId\" " +
            $"WHERE t.\"JobId\"='{transcodeJob}' AND r.\"Status\"='Ready' AND r.\"Size\">0;")
            .Should().Be("1");
        fixture.VerifyStoredOutputs("alexandria-streaming", audio.VersionId + "/a/opus/");

        fixture.RequestPlaylistSync(audio.FileId);
        fixture.WaitForArtistPlaylist("Fixture Artist");
        fixture.VerifyPeriodicPlaylistSweep();
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Dependency", "Internet")]
    public void Lyrics_real_lookup_retries_provider_outage_then_records_not_found_and_acknowledges_delivery()
    {
        using var fixture = new CachedWorkerProcessingFixture(lyrics: true);
        fixture.PrepareAndStart();
        var audio = fixture.SeedFile("lyrics-audio.wav", "audio/wav", CreateWave());
        fixture.WaitForJob(fixture.QueuePreview(audio.VersionId, "media.wav"));
        fixture.Sql($"UPDATE \"MediaMetadata\" SET \"Title\"='Alexandria nonexistent song {Guid.NewGuid():N}' WHERE \"FileId\"='{audio.FileId}';");
        var transcodeJob = fixture.QueueTranspilation(audio.VersionId);
        fixture.WaitForJob(transcodeJob);

        fixture.SetLyricsProviderAvailability(false);
        var lookup = fixture.QueueLyricsLookup(transcodeJob);
        fixture.WaitForLyricsProviderRetry(lookup);
        fixture.SetLyricsProviderAvailability(true);
        fixture.WaitForLyricsNoMatch(lookup);

        fixture.Sql($"SELECT \"Status\" FROM \"TrackLyrics\" WHERE \"JobId\"='{lookup}';").Should().Be("4");
        fixture.VerifyLyricsAcknowledged();
    }

    private static byte[] CreateWave()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(8000);
        writer.Write(16000);
        writer.Write((short)2);
        writer.Write((short)16);

        using var tags = new MemoryStream();
        using (var tagWriter = new BinaryWriter(tags, Encoding.ASCII, leaveOpen: true))
        {
            tagWriter.Write(Encoding.ASCII.GetBytes("INFO"));
            foreach (var (key, value) in new[] { ("IART", "Fixture Artist"), ("INAM", "Fixture Song"), ("IPRD", "Fixture Album") })
            {
                var bytes = Encoding.ASCII.GetBytes(value + '\0');
                tagWriter.Write(Encoding.ASCII.GetBytes(key));
                tagWriter.Write(bytes.Length);
                tagWriter.Write(bytes);
                if (bytes.Length % 2 != 0) tagWriter.Write((byte)0);
            }
        }

        writer.Write(Encoding.ASCII.GetBytes("LIST"));
        writer.Write((int)tags.Length);
        writer.Write(tags.ToArray());
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(32000);
        for (var i = 0; i < 16000; i++)
            writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / 8000) * 4000));
        stream.Position = 4;
        writer.Write((int)stream.Length - 8);
        return stream.ToArray();
    }

    private static byte[] CreatePdf()
    {
        const string content = "BT /F1 12 Tf 20 100 Td (Alexandria worker fixture) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 150] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var start = pdf.Length;
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(start).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
