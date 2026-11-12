using Alexandria.Data.Audit;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Audit;

public class AuditSqlClassifierTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("  select * from \"Files\"")]
    [InlineData("-- tagged query\nSELECT * FROM \"Files\"")]
    [InlineData("-- one\n-- two\n  SELECT 1")]
    [InlineData("")]
    public void IsRead_selects_returnsTrue(string sql)
    {
        AuditSqlClassifier.IsRead(sql).Should().BeTrue();
    }

    [Theory]
    [InlineData("UPDATE \"Files\" SET \"DirectoryId\" = @p0")]
    [InlineData("INSERT INTO \"Files\" VALUES (1)")]
    [InlineData("DELETE FROM \"Files\"")]
    [InlineData("WITH moved AS (UPDATE \"Files\" SET x = 1 RETURNING *) SELECT * FROM moved")]
    [InlineData("-- tagged\nUPDATE \"Files\" SET x = 1")]
    public void IsRead_writesAndCtes_returnsFalse(string sql)
    {
        AuditSqlClassifier.IsRead(sql).Should().BeFalse();
    }
}
