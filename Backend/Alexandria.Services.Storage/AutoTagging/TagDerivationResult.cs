using Alexandria.Data.Models;
using Alexandria.Dto.Autotag;

namespace Alexandria.Services.Storage.AutoTagging;

/// <summary>
/// Outcome of deriving candidates from one enrichment payload: the candidates to apply
/// plus the model-emitted keys that had no matching <see cref="Tag.ExternalKey"/> in the
/// seeded taxonomy (drift — the caller decides how to surface them).
/// </summary>
public sealed record TagDerivationResult(
    IReadOnlyList<TagCandidate> Candidates,
    IReadOnlyList<string> UnknownKeys);