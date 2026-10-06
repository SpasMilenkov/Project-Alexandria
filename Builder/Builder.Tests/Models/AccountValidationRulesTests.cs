using Alexandria.Common.Auth;
using AwesomeAssertions;

namespace Builder.Tests.Models;

public class AccountValidationRulesTests
{
    [Theory]
    [InlineData("Aa1!xy", true)]
    [InlineData("Long-enough-Password1!", true)]
    [InlineData("Aa1!", false)]
    [InlineData("alllowercase1!", false)]
    [InlineData("ALLUPPERCASE1!", false)]
    [InlineData("NoNumber!", false)]
    [InlineData("NoSymbol1", false)]
    [InlineData("ÄÄÄäää١!", false)]
    [InlineData("Password1!\nBAD=value", false)]
    public void ValidatePassword_backendPolicyAndControlCharacters_areEnforced(string password, bool accepted)
    {
        (AccountValidationRules.ValidatePassword(password) is null).Should().Be(accepted);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("owner.name+tag_1-@", true)]
    [InlineData("name with spaces", false)]
    [InlineData("café", false)]
    [InlineData("", false)]
    public void ValidateUsername_identityCharacterSet_isEnforced(string username, bool accepted)
    {
        (AccountValidationRules.ValidateUsername(username) is null).Should().Be(accepted);
    }

    [Theory]
    [InlineData("owner@example.com", true)]
    [InlineData("owner", false)]
    [InlineData("a@b@c", false)]
    [InlineData("owner@example.com\nX=value", false)]
    public void ValidateEmail_signInAddress_isValidated(string email, bool accepted)
    {
        (AccountValidationRules.ValidateEmail(email) is null).Should().Be(accepted);
    }
}
