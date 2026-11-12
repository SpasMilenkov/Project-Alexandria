using Alexandria.Api.Features.Activity.ExportActivity;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Audit;

public class AuditExportRequestValidatorTests
{
    private static bool IsValid(ActivityExportRequest request) =>
        new ActivityExportRequestValidator().Validate(request).IsValid;

    [Fact]
    public void Validate_noBounds_isValid()
    {
        IsValid(new ActivityExportRequest()).Should().BeTrue();
    }

    [Fact]
    public void Validate_completeBounds_isValid()
    {
        IsValid(new ActivityExportRequest
        {
            StartAt = DateTimeOffset.UtcNow,
            StartId = Guid.NewGuid(),
            EndAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndId = Guid.NewGuid(),
        }).Should().BeTrue();
    }

    [Fact]
    public void Validate_halfAStartBound_isRejected()
    {
        IsValid(new ActivityExportRequest { StartAt = DateTimeOffset.UtcNow }).Should().BeFalse();
        IsValid(new ActivityExportRequest { StartId = Guid.NewGuid() }).Should().BeFalse();
    }

    [Fact]
    public void Validate_halfAnEndBound_isRejected()
    {
        IsValid(new ActivityExportRequest { EndAt = DateTimeOffset.UtcNow }).Should().BeFalse();
        IsValid(new ActivityExportRequest { EndId = Guid.NewGuid() }).Should().BeFalse();
    }
}
