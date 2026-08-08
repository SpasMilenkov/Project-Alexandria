using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Autotag;

/// <summary>
/// A single derived auto-tag candidate: a taxonomy tag that should be applied to a
/// file, with the confidence the model assigned to it. For genre, the parent rollup
/// candidates carry the max confidence among their matched children.
/// </summary>
public sealed record TagCandidate(Guid TagId, double Confidence, TagFacet Facet);