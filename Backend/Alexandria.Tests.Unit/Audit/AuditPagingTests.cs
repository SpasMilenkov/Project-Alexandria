using Alexandria.Repositories;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Audit;

public class AuditPagingTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(39, 20, 2)]
    [InlineData(40, 20, 2)]
    public void TotalPages_roundsUp(int count, int pageSize, int expected)
    {
        AuditLogRepository.TotalPages(count, pageSize).Should().Be(expected);
    }
}
