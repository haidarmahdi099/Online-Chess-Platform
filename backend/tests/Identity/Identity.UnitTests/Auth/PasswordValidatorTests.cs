using Identity.Application.Common;

namespace Identity.UnitTests.Auth;

public sealed class PasswordValidatorTests
{
    [Theory]
    [InlineData("short1")]
    [InlineData("1234567890")]
    [InlineData("password123")]
    [InlineData("letters_only")]
    public void Validate_RejectsWeakPasswords(string password)
    {
        var exception = Assert.Throws<Identity.Application.Common.ApplicationException>(() => PasswordValidator.Validate(password));

        Assert.Equal("password_invalid", exception.Code);
    }

    [Fact]
    public void Validate_AcceptsPasswordMeetingRequirements()
    {
        PasswordValidator.Validate("correct-horse-92");
    }
}
