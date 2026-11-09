using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents;

public class PdfMetadataParsingTests
{
    // Captured from pdfinfo 26.01 with -isodates against a LibreOffice export.
    private const string InfoOutput = """
        Title:           Quarterly report
        Author:          Ana Doe
        Creator:         Writer
        Producer:        LibreOffice 24.2
        CreationDate:    2026-01-08T20:26:20+02
        Custom Metadata: no
        Metadata Stream: yes
        Tagged:          yes
        Form:            none
        JavaScript:      no
        Pages:           3
        Encrypted:       no
        Page size:       595.304 x 841.89 pts (A4)
        File size:       243043 bytes
        PDF version:     1.7
        """;

    private const string XmpPacket = """
        <?xpacket begin='﻿' id='W5M0MpCehiHzreSzNTczkc9d'?>
        <x:xmpmeta xmlns:x='adobe:ns:meta/'>
        <rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
        <rdf:Description rdf:about="" xmlns:pdf='http://ns.adobe.com/pdf/1.3/'><pdf:Producer>GPL Ghostscript 10.06.0</pdf:Producer>
        <pdf:Keywords>budget, q3</pdf:Keywords>
        </rdf:Description>
        <rdf:Description rdf:about="" xmlns:xmp='http://ns.adobe.com/xap/1.0/'><xmp:CreateDate>2026-11-09T15:29:58Z</xmp:CreateDate></rdf:Description>
        <rdf:Description rdf:about="" xmlns:xmpMM='http://ns.adobe.com/xap/1.0/mm/' xmpMM:DocumentID='uuid:f8bee66d-146e-11fd-0000-9251ee614955'/>
        <rdf:Description rdf:about="" xmlns:d='http://purl.org/dc/elements/1.1/'>
          <d:title><rdf:Alt><rdf:li xml:lang='x-default'>Quarterly report</rdf:li></rdf:Alt></d:title>
          <d:creator><rdf:Seq><rdf:li>Ana Doe</rdf:li><rdf:li>Ben Roe</rdf:li></rdf:Seq></d:creator>
          <d:language><rdf:Bag><rdf:li>en-GB</rdf:li></rdf:Bag></d:language>
        </rdf:Description>
        </rdf:RDF>
        </x:xmpmeta>
        <?xpacket end='w'?>
        """;

    [Fact]
    public void ParseInfo_knownLabels_becomeFields()
    {
        var fields = new DocumentFieldBag();

        PdfInfoReader.ParseInfo(InfoOutput, fields);

        fields.Values["pdf:Title"].Should().Be("Quarterly report");
        fields.Values["pdf:CreationDate"].Should().Be("2026-01-08T20:26:20+02");
        fields.Values["pdf:Page size"].Should().Be("595.304 x 841.89 pts (A4)");
        fields.Values["pdf:Metadata Stream"].Should().Be("yes");
    }

    [Fact]
    public void ParseInfo_titleWithLineBreak_staysOneFieldAndInventsNoOther()
    {
        const string output = "Title:           First line\nSecret: not a label\nAuthor:          Ana Doe\nPages:           1\n";
        var fields = new DocumentFieldBag();

        PdfInfoReader.ParseInfo(output, fields);

        fields.Values["pdf:Title"].Should().Be("First line\nSecret: not a label");
        fields.Values.Should().NotContainKey("pdf:Secret");
        fields.Values["pdf:Author"].Should().Be("Ana Doe");
    }

    [Fact]
    public void Xmp_descriptionAttributesAndElements_areFlattenedWithStablePrefixes()
    {
        var fields = new DocumentFieldBag();

        XmpReader.ReadInto(XmpPacket, fields, NullLogger.Instance);

        fields.Values["xmp:pdf:Producer"].Should().Be("GPL Ghostscript 10.06.0");
        fields.Values["xmp:xmp:CreateDate"].Should().Be("2026-11-09T15:29:58Z");
        fields.Values["xmp:xmpMM:DocumentID"].Should().Be("uuid:f8bee66d-146e-11fd-0000-9251ee614955");
        fields.Values["xmp:dc:title"].Should().Be("Quarterly report");
        fields.Values["xmp:dc:language"].Should().Be("en-GB");
    }

    [Fact]
    public void Xmp_listValues_areJoined()
    {
        var fields = new DocumentFieldBag();

        XmpReader.ReadInto(XmpPacket, fields, NullLogger.Instance);

        fields.Values["xmp:dc:creator"].Should().Be("Ana Doe; Ben Roe");
    }

    [Fact]
    public void Xmp_packetThatIsNotXml_isIgnored()
    {
        var fields = new DocumentFieldBag();

        XmpReader.ReadInto("\u0001ÿ garbage from an encrypted stream", fields, NullLogger.Instance);

        fields.Values.Should().BeEmpty();
    }

    [Fact]
    public void Xmp_oversizedPacket_isIgnored()
    {
        var fields = new DocumentFieldBag();
        var packet = XmpPacket + new string(' ', XmpReader.MaxPacketChars);

        XmpReader.ReadInto(packet, fields, NullLogger.Instance);

        fields.Values.Should().BeEmpty();
    }
}
