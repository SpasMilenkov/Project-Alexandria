using System.IO.Compression;
using System.Text;

namespace Alexandria.Tests.Unit.Documents;

/// <summary>
/// Builds small zip packages in memory, so the Office and OpenDocument readers can be tested
/// against exact part contents.
/// </summary>
internal static class PackageBuilder
{
    public static MemoryStream Build(params (string Path, string Content)[] parts)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in parts)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    public const string PackageRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    public const string CoreProperties = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
            xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:title>Quarterly report</dc:title>
          <dc:subject>Budget</dc:subject>
          <dc:creator>Ana Doe</dc:creator>
          <cp:keywords>budget, q3</cp:keywords>
          <cp:lastModifiedBy>Ben Roe</cp:lastModifiedBy>
          <cp:revision>4</cp:revision>
          <dc:language>en-US</dc:language>
          <dcterms:created xsi:type="dcterms:W3CDTF">2024-03-01T10:20:00Z</dcterms:created>
          <dcterms:modified xsi:type="dcterms:W3CDTF">2024-03-05T08:00:00Z</dcterms:modified>
        </cp:coreProperties>
        """;

    public const string ExtendedProperties = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
            xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
          <Template>Normal.dotm</Template>
          <TotalTime>42</TotalTime>
          <Pages>3</Pages>
          <Words>1200</Words>
          <Application>Microsoft Office Word</Application>
          <Company>Acme</Company>
          <TitlesOfParts><vt:vector size="1" baseType="lpstr"><vt:lpstr>Quarterly report</vt:lpstr></vt:vector></TitlesOfParts>
        </Properties>
        """;
}
