using Identity.Application.Abstractions;
using Identity.Application.Auth.Logout;
using Identity.Application.Auth.Refresh;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Moq;

namespace Identity.UnitTests.Auth;

public sealed class RefreshAndLogoutHandlerTests
{
    [Fact]
    public async Task Refresh_RotatesActiveRefreshToken()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");
        var oldToken = RefreshToken.Create(user.Id, "old-refresh", DateTime.UtcNow.AddDays(1));
        RefreshToken? replacement = null;
        var refreshTokens = new Mock<IRefreshTokenRepository>();
        refreshTokens.Setup(x => x.GetByTokenAsync("old-refresh", It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        refreshTokens.Setup(x => x.RotateAsync(oldToken.Id, It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, RefreshToken, CancellationToken>((_, token, _) => replacement = token)
            .ReturnsAsync(true);
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(x => x.CreateAccessToken(user.Id, user.Username, user.Role)).Returns("new-access");
        tokenService.Setup(x => x.CreateRefreshToken()).Returns("new-refresh");
        var handler = new RefreshTokenHandler(refreshTokens.Object, users.Object, tokenService.Object);

        var result = await handler.HandleAsync(new RefreshTokenCommand(" old-refresh "));

        Assert.Equal("new-access", result.AccessToken);
        Assert.Equal("new-refresh", result.RefreshToken);
        Assert.NotNull(replacement);
        Assert.Equal(RefreshToken.HashToken("new-refresh"), replacement.Token);
        Assert.NotEqual(oldToken.Id, replacement.Id);
    }

    [Fact]
    public async Task Refresh_ReusedTokenRevokesAllSessions()
    {
        var userId = Guid.NewGuid();
        var replayedToken = RefreshToken.Create(userId, "replayed", DateTime.UtcNow.AddDays(1));
        replayedToken.Revoke();
        var refreshTokens = new Mock<IRefreshTokenRepository>();
        refreshTokens.Setup(x => x.GetByTokenAsync("replayed", It.IsAny<CancellationToken>())).ReturnsAsync(replayedToken);
        var handler = new RefreshTokenHandler(refreshTokens.Object, new Mock<IUserRepository>().Object, new Mock<ITokenService>().Object);

        var exception = await Assert.ThrowsAsync<Identity.Application.Common.ApplicationException>(
            () => handler.HandleAsync(new RefreshTokenCommand("replayed")));

        Assert.Equal("invalid_refresh_token", exception.Code);
        refreshTokens.Verify(x => x.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_RevokesActiveTokenAndIsSafeToRepeat()
    {
        var active = RefreshToken.Create(Guid.NewGuid(), "active", DateTime.UtcNow.AddDays(1));
        var repository = new Mock<IRefreshTokenRepository>();
        repository.Setup(x => x.GetByTokenAsync("active", It.IsAny<CancellationToken>())).ReturnsAsync(active);
        var handler = new LogoutHandler(repository.Object);

        await handler.HandleAsync(new LogoutCommand("active"));
        await handler.HandleAsync(new LogoutCommand("active"));

        Assert.False(active.IsActive());
        repository.Verify(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_UnknownTokenDoesNotRevealWhetherItExists()
    {
        var repository = new Mock<IRefreshTokenRepository>();
        repository.Setup(x => x.GetByTokenAsync("unknown", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);
        var handler = new LogoutHandler(repository.Object);

        await handler.HandleAsync(new LogoutCommand("unknown"));

        repository.Verify(x => x.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
