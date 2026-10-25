using Identity.Application.Abstractions;
using Identity.Application.Auth.Login;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Moq;

namespace Identity.UnitTests.Auth;

public sealed class LoginUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidVerifiedUser_IssuesTokensAndResetsCounters()
    {
        var user = CreateVerifiedUser();
        for (var i = 0; i < 2; i++)
            user.RecordFailedLogin();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUsernameAsync("Alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Verify("good-password-92", "password-hash")).Returns(true);
        var tokens = new Mock<ITokenService>();
        tokens.Setup(x => x.CreateAccessToken(user.Id, user.Username, user.Role)).Returns("access-token");
        tokens.Setup(x => x.CreateRefreshToken()).Returns("refresh-token");
        RefreshToken? savedRefresh = null;
        var refreshTokens = new Mock<IRefreshTokenRepository>();
        refreshTokens.Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => savedRefresh = token)
            .Returns(Task.CompletedTask);
        var attempts = new Mock<ILoginAttemptRepository>();
        attempts.Setup(x => x.GetByIpAddressAsync("127.0.0.1", It.IsAny<CancellationToken>())).ReturnsAsync((LoginAttempt?)null);
        var handler = new LoginUserHandler(users.Object, hasher.Object, tokens.Object, refreshTokens.Object, attempts.Object);

        var result = await handler.HandleAsync(new LoginUserCommand(" Alice ", "good-password-92", "127.0.0.1"));

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.NotNull(savedRefresh);
        Assert.Equal(RefreshToken.HashToken("refresh-token"), savedRefresh.Token);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Equal(0, user.LockoutCount);
        users.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnverifiedUser_IsRejectedWithoutIssuingTokens()
    {
        var user = User.Create("Alice", "alice@example.com", "password-hash");
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUsernameAsync("Alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Verify(It.IsAny<string>(), user.PasswordHash)).Returns(true);
        var tokenService = new Mock<ITokenService>();
        var handler = CreateHandler(users, hasher, tokenService);

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new LoginUserCommand("Alice", "good-password-92", "127.0.0.1")));

        Assert.Equal("email_not_verified", exception.Code);
        tokenService.Verify(x => x.CreateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_RecordsUserAndIpFailures()
    {
        var user = CreateVerifiedUser();
        LoginAttempt? savedAttempt = null;
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByUsernameAsync("Alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Verify("wrong-password", user.PasswordHash)).Returns(false);
        var attempts = new Mock<ILoginAttemptRepository>();
        attempts.Setup(x => x.GetByIpAddressAsync("127.0.0.1", It.IsAny<CancellationToken>())).ReturnsAsync((LoginAttempt?)null);
        attempts.Setup(x => x.AddAsync(It.IsAny<LoginAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<LoginAttempt, CancellationToken>((attempt, _) => savedAttempt = attempt)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler(users, hasher, new Mock<ITokenService>(), loginAttempts: attempts);

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new LoginUserCommand("Alice", "wrong-password", "127.0.0.1")));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.NotNull(savedAttempt);
        Assert.Equal(1, savedAttempt.FailedAttempts);
    }

    [Fact]
    public async Task HandleAsync_LockedIpRejectsBeforeLookingUpUser()
    {
        var lockedAttempt = LoginAttempt.Create("127.0.0.1");
        for (var i = 0; i < 10; i++)
            lockedAttempt.RecordFailedAttempt();
        var users = new Mock<IUserRepository>();
        var attempts = new Mock<ILoginAttemptRepository>();
        attempts.Setup(x => x.GetByIpAddressAsync("127.0.0.1", It.IsAny<CancellationToken>())).ReturnsAsync(lockedAttempt);
        var handler = CreateHandler(users, new Mock<IPasswordHasher>(), new Mock<ITokenService>(), loginAttempts: attempts);

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new LoginUserCommand("Alice", "wrong-password", "127.0.0.1")));

        Assert.Equal("too_many_attempts", exception.Code);
        users.Verify(x => x.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateVerifiedUser()
    {
        var user = User.Create("Alice", "alice@example.com", "password-hash");
        user.SetEmailVerification("verified", DateTime.UtcNow.AddHours(1));
        Assert.True(user.VerifyEmail("verified"));
        return user;
    }

    private static LoginUserHandler CreateHandler(
        Mock<IUserRepository> users,
        Mock<IPasswordHasher> hasher,
        Mock<ITokenService> tokenService,
        Mock<IRefreshTokenRepository>? refreshTokens = null,
        Mock<ILoginAttemptRepository>? loginAttempts = null) =>
        new(users.Object, hasher.Object, tokenService.Object,
            (refreshTokens ?? new Mock<IRefreshTokenRepository>()).Object,
            (loginAttempts ?? new Mock<ILoginAttemptRepository>()).Object);
}
