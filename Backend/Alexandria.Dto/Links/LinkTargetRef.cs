using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Links;

/// <summary>The kind and id parsed from one <c>alexandria://</c> link.</summary>
public sealed record LinkTargetRef(LinkTargetKind Kind, Guid Id);
