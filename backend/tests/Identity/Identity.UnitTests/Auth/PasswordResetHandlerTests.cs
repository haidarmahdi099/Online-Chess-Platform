using Identity.Application.Abstractions;
using Identity.Application.Auth.ForgotPassword;
using Identity.Application.Auth.ResetPassword;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Identity.UnitTests.Auth;

public sealed class PasswordResetHandlerTests
{
    [Fact]
    public async Task ForgotPassword_KnownAccountStoresOnlyHashAndEmailsRawToken()
    {
        var user = User.Create("Alice", "alice@example.com", "password-hash");
        PasswordResetToken? savedToken = null;
        string? emailedToken = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var resetTokens = new Mock<IPasswordResetTokenRepository>();
        resetTokens.Setup(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((token, _) => savedToken = token)
            .Returns(Task.CompletedTask);
        var tokenService = new Mock<IPasswordResetTokenService>();
        tokenService.Setup(x => x.GenerateToken()).Returns("raw-reset-token");
        tokenService.Setup(x => x.HashToken("raw-reset-token")).Returns("hashed-reset-token");
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.SendPasswordResetEmailAsync(user.Email, "raw-reset-token", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, token, _) => emailedToken = token)
            .Returns(Task.CompletedTask);
        var handler = new ForgotPasswordHandler(users.Object, resetTokens.Object, tokenService.Object, email.Object, NullLogger<ForgotPasswordHandler>.Instance);

        await handler.HandleAsync(new ForgotPasswordCommand(" ALICE@EXAMPLE.COM "));

        Assert.NotNull(savedToken);
        Assert.Equal("hashed-reset-token", savedToken.TokenHash);
        Assert.NotEqual("raw-reset-token", savedToken.TokenHash);
        Assert.Equal("raw-reset-token", emailedToken);
        resetTokens.Verify(x => x.InvalidateActiveTokensAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_UnknownAccountDoesNotRevealOrCreateToken()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var resetTokens = new Mock<IPasswordResetTokenRepository>();
        var email = new Mock<IEmailSender>();
        var handler = new ForgotPasswordHandler(users.Object, resetTokens.Object, new Mock<IPasswordResetTokenService>().Object,
            email.Object, NullLogger<ForgotPasswordHandler>.Instance);

        await handler.HandleAsync(new ForgotPasswordCommand("missing@example.com"));

        resetTokens.Verify(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_ActiveTokenChangesPasswordAndConsumesToken()
    {
        var user = User.Create("Alice", "alice@example.com", "old-hash");
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();
        var resetToken = PasswordResetToken.Create(user.Id, "token-hash", DateTime.UtcNow.AddMinutes(10));
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var resetTokens = new Mock<IPasswordResetTokenRepository>();
        resetTokens.Setup(x => x.GetByTokenHashAsync("token-hash", It.IsAny<CancellationToken>())).ReturnsAsync(resetToken);
        resetTokens.Setup(x => x.TryCompleteResetAsync(user, resetToken.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tokenService = new Mock<IPasswordResetTokenService>();
        tokenService.Setup(x => x.HashToken("raw-token")).Returns("token-hash");
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Hash("new-password-92")).Returns("new-hash");
        var handler = new ResetPasswordHandler(users.Object, resetTokens.Object, tokenService.Object, hasher.Object);

        await handler.HandleAsync(new ResetPasswordCommand(" Alice@Example.com ", " raw-token ", "new-password-92"));

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut());
        resetTokens.Verify(x => x.TryCompleteResetAsync(user, resetToken.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ExpiredTokenIsRejected()
    {
        var user = User.Create("Alice", "alice@example.com", "old-hash");
        var expired = PasswordResetToken.Create(user.Id, "token-hash", DateTime.UtcNow.AddSeconds(-1));
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var tokens = new Mock<IPasswordResetTokenRepository>();
        tokens.Setup(x => x.GetByTokenHashAsync("token-hash", It.IsAny<CancellationToken>())).ReturnsAsync(expired);
        var service = new Mock<IPasswordResetTokenService>();
        service.Setup(x => x.HashToken("token")).Returns("token-hash");
        var handler = new ResetPasswordHandler(users.Object, tokens.Object, service.Object, new Mock<IPasswordHasher>().Object);

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new ResetPasswordCommand(user.Email, "token", "new-password-92")));

        Assert.Equal("invalid_reset_token", exception.Code);
        tokens.Verify(x => x.TryCompleteResetAsync(It.IsAny<User>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
