using Alexandria.Api.Features.Storage.Files.GetContentMatches;
using AwesomeAssertions;
using FluentValidation.Results;

namespace Alexandria.Tests.Unit.Storage;

public class GetContentMatchesRequestValidatorTests
{
    private static ValidationResult Validate(string query, Guid? fileId = null) =>
        new GetContentMatchesRequestValidator().Validate(new GetContentMatchesRequest
        {
            FileId = fileId ?? Guid.NewGuid(),
            Query = query
        });

    [Fact]
    public void Validate_ordinaryQuery_isValid()
    {
        Validate("hotel invoice paris").IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_blankQuery_isRejected(string query)
    {
        Validate(query).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_queryLongerThanTheSearchBox_isRejected()
    {
        Validate(new string('a', 201)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_emptyFileId_isRejected()
    {
        Validate("invoice", Guid.Empty).IsValid.Should().BeFalse();
    }
}