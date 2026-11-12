using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed class FailedSignInReasonCount
{
    public required LoginFailureReason Reason { get; init; }
    public required int Count { get; init; }
}
