using Identity.Application.Abstractions;
using Identity.Application.Auth.Register;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Identity.UnitTests.Auth;

public sealed class RegisterUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesEmailPersistsUserAndSendsVerification()
    {
        User? savedUser = null;
        string? emailBody = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.ExistsByUsernameAsync("Alice_1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        users.Setup(x => x.ExistsByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => savedUser = user)
            .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Hash("good-password-92")).Returns("hashed-password");
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.SendEmailAsync("alice@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => emailBody = body)
            .Returns(Task.CompletedTask);

        var handler = new RegisterUserHandler(users.Object, hasher.Object, email.Object, NullLogger<RegisterUserHandler>.Instance, TestConfiguration());

        var result = await handler.HandleAsync(new RegisterUserCommand(" Alice_1 ", " Alice@Example.com ", "good-password-92"));

        Assert.NotNull(savedUser);
        Assert.Equal("Alice_1", savedUser.Username);
        Assert.Equal("alice@example.com", savedUser.Email);
        Assert.Equal("hashed-password", savedUser.PasswordHash);
        Assert.False(savedUser.EmailVerified);
        Assert.Equal(savedUser.Id, result.UserId);
        Assert.True(result.EmailVerificationRequired);
        Assert.NotNull(emailBody);
        Assert.Contains("one-time code", emailBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alice_1", emailBody, StringComparison.Ordinal);
        Assert.Matches("(?<!\\d)\\d{6}(?!\\d)", emailBody);
        Assert.DoesNotContain("<script>", emailBody);
    }

    [Theory]
    [InlineData("ab", "alice@example.com", "good-password-92", "username_invalid")]
    [InlineData("Alice", "not-an-email", "good-password-92", "email_invalid")]
    [InlineData("Alice", "alice@example.com", "short1", "password_invalid")]
    public async Task HandleAsync_InvalidInputFailsBeforePersistence(
        string username,
        string email,
        string password,
        string expectedCode)
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var sender = new Mock<IEmailSender>();
        var handler = new RegisterUserHandler(users.Object, hasher.Object, sender.Object, NullLogger<RegisterUserHandler>.Instance, TestConfiguration());

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new RegisterUserCommand(username, email, password)));

        Assert.Equal(expectedCode, exception.Code);
        users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmailFailureDoesNotLoseCreatedAccount()
    {
        User? savedUser = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        users.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => savedUser = user)
            .Returns(Task.CompletedTask);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hash");
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mail unavailable"));
        var handler = new RegisterUserHandler(users.Object, hasher.Object, sender.Object, NullLogger<RegisterUserHandler>.Instance, TestConfiguration());

        var result = await handler.HandleAsync(new RegisterUserCommand("Alice", "alice@example.com", "good-password-92"));

        Assert.NotNull(savedUser);
        Assert.Equal(savedUser.Id, result.UserId);
        users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IConfiguration TestConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SecretKey"] = "unit-test-signing-secret-with-at-least-32-characters" })
        .Build();
}
