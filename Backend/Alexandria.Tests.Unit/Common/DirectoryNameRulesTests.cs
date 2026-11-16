using Alexandria.Common.Validation;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Common;

public class DirectoryNameRulesTests
{
    [Theory]
    [InlineData("Music")]
    [InlineData("Live 2020 (Remastered)")]
    [InlineData("Ünïcødé & friends")]
    public void FindProblem_validName_returnsNull(string name)
    {
        DirectoryNameRules.FindProblem(name).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindProblem_blankName_isRejected(string? name)
    {
        DirectoryNameRules.FindProblem(name).Should().Contain("empty");
    }

    [Fact]
    public void FindProblem_tooLong_isRejected()
    {
        DirectoryNameRules.FindProblem(new string('a', DirectoryNameRules.MaxLength + 1))
            .Should().Contain("exceed");
        DirectoryNameRules.FindProblem(new string('a', DirectoryNameRules.MaxLength)).Should().BeNull();
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("Live: 2020")]
    [InlineData("why?")]
    [InlineData("star*")]
    [InlineData("\"quoted\"")]
    [InlineData("<tag>")]
    [InlineData("pipe|d")]
    public void FindProblem_forbiddenCharacter_isRejected(string name)
    {
        DirectoryNameRules.FindProblem(name).Should().Contain("cannot contain");
    }
}
