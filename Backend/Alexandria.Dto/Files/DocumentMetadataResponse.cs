namespace Alexandria.Dto.Files;

public sealed class DocumentMetadataResponse
{
    public required Guid FileVersionId { get; init; }

    /// <summary>
    /// False when the version was not read yet or cannot be read, for example because it is
    /// encrypted. Empty fields then mean "unknown", not "this file has none".
    /// </summary>
    public required bool IsExtracted { get; init; }

    /// <summary>
    /// True when the file names a person or company, which anyone who downloads a shared copy
    /// can read.
    /// </summary>
    public required bool HasPersonalDetails { get; init; }

    public required bool IsPasswordProtected { get; init; }

    /// <summary>
    /// True for an ebook whose pages a store's copy protection encrypts. Its properties are
    /// still read.
    /// </summary>
    public required bool IsDrmProtected { get; init; }

    public string? Title { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }
    public string? Language { get; init; }

    public string? Author { get; init; }
    public string? LastModifiedBy { get; init; }
    public string? Company { get; init; }

    public string? Application { get; init; }
    public string? Producer { get; init; }

    /// <summary>
    /// A true instant only when <see cref="AuthoredOffsetMinutes"/> is set. Otherwise this is
    /// the author's wall clock, despite being serialized as UTC.
    /// </summary>
    public DateTime? AuthoredAt { get; init; }
    public short? AuthoredOffsetMinutes { get; init; }

    /// <inheritdoc cref="AuthoredAt"/>
    public DateTime? ModifiedAt { get; init; }
    public short? ModifiedOffsetMinutes { get; init; }

    public int? PageCount { get; init; }
    public int? WordCount { get; init; }
    public int? SlideCount { get; init; }
    public int? SheetCount { get; init; }
    public int? EditingMinutes { get; init; }
    public string? PaperSize { get; init; }

    public string? Publisher { get; init; }

    /// <summary>The 13-digit form, whatever form the file used.</summary>
    public string? Isbn { get; init; }

    public string? Series { get; init; }
    public decimal? SeriesIndex { get; init; }
}
