using Alexandria.Dto.Links;

namespace Alexandria.Common.Services;

/// <summary>
/// Looks up what <c>alexandria://</c> links in a note point at, as the viewer is allowed to see it.
/// Notes store only ids, so names shown in a note are always read live.
/// </summary>
public interface ILinkTargetService
{
    /// <summary>
    /// One entry per distinct target, in request order. A target the viewer can't see is
    /// <see cref="LinkTargetState.Unavailable"/> whether it is missing or someone else's.
    /// </summary>
    Task<IReadOnlyList<LinkTargetDto>> ResolveAsync(IReadOnlyCollection<LinkTargetRef> targets, Guid userId,
        CancellationToken ct = default);
}
