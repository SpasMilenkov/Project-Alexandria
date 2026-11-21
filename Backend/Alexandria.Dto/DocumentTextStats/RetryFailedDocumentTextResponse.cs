namespace Alexandria.Dto.DocumentTextStats;

/// <summary>How many failed documents were queued for another attempt.</summary>
public sealed record RetryFailedDocumentTextResponse(int Reset);