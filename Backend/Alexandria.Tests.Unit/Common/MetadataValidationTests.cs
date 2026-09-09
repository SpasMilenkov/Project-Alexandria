using Alexandria.Common.Validation;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Common;

public class MetadataValidationTests
{
    [Theory]
    [InlineData("1999")]
    [InlineData("2000")]
    public void IsValidYear_wellFormedPastYears(string year)
    {
        MetadataValidation.IsValidYear(year).Should().BeTrue();
    }

    [Fact]
    public void IsValidYear_currentAndNextYear_areValid()
    {
        var current = DateTime.UtcNow.Year;
        MetadataValidation.IsValidYear(current.ToString()).Should().BeTrue();
        MetadataValidation.IsValidYear((current + 1).ToString()).Should().BeTrue();
    }

    [Theory]
    [InlineData("2221")]
    [InlineData("3000")]
    [InlineData("0000")]
    [InlineData("0999")]
    [InlineData("99")]
    [InlineData("19999")]
    [InlineData("abcd")]
    [InlineData("19 9")]
    [InlineData("+199")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidYear_malformedOrOutOfRange_isInvalid(string year)
    {
        MetadataValidation.IsValidYear(year).Should().BeFalse();
    }

    [Fact]
    public void IsValidYear_null_isInvalid()
    {
        MetadataValidation.IsValidYear(null).Should().BeFalse();
    }

    [Fact]
    public void IsValidYear_farFuture_isInvalid()
    {
        MetadataValidation.IsValidYear((DateTime.UtcNow.Year + 2).ToString()).Should().BeFalse();
    }
}