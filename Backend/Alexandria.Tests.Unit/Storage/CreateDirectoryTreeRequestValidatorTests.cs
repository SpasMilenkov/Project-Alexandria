using Alexandria.Api.Features.Storage.Directories.CreateDirectoryTree;
using AwesomeAssertions;
using FluentValidation.Results;

namespace Alexandria.Tests.Unit.Storage;

public class CreateDirectoryTreeRequestValidatorTests
{
    private static ValidationResult Validate(params string[] paths) =>
        new CreateDirectoryTreeRequestValidator().Validate(new CreateDirectoryTreeRequest { Paths = [.. paths] });

    [Fact]
    public void Validate_plainFolderUpload_isValid()
    {
        Validate("Album/Disc 1/01 Intro.mp3", "Album/cover.jpg").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_folderWithForbiddenCharacter_namesThePath()
    {
        var result = Validate("Album/Live: 2020?/01 Intro.mp3");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Album/Live: 2020?/01 Intro.mp3").And.Contain("cannot contain");
    }

    [Fact]
    public void Validate_fileNameWithFolderOnlyCharacters_isValid()
    {
        Validate("Album/Track: Live?.mp3").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_emptyFolderSegment_isRejected()
    {
        Validate("Album//01 Intro.mp3").IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_fileNameTooLong_isRejected()
    {
        Validate($"Album/{new string('a', 256)}.mp3").IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("/Album/a.mp3")]
    [InlineData("Album/../a.mp3")]
    public void Validate_nonRelativePath_isRejected(string path)
    {
        Validate(path).IsValid.Should().BeFalse();
    }
}
