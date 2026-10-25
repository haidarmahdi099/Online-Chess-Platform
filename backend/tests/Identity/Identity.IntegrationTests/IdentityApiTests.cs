using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Identity.IntegrationTests;

public sealed class IdentityApiTests(IdentityApiFixture fixture) : IClassFixture<IdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "GoodPass123!";

    [Fact]
    public async Task HealthAndAnonymousRoutes_AreAvailable_AndProtectedRoutesRequireAuthentication()
    {
        using var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/social/friends")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/users/search?q=x")).StatusCode);
    }

    [Fact]
    public async Task RegisterVerifyLoginReadProfileRotateRefreshAndLogout_WorksEndToEnd()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);

        using var login = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var first = await ReadTokensAsync(login);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using var me = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using (var profile = await JsonDocument.ParseAsync(await me.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(account.Username, profile.RootElement.GetProperty("username").GetString());
            Assert.Equal(account.Email, profile.RootElement.GetProperty("email").GetString());
        }

        using var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var second = await ReadTokensAsync(refresh);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        using var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var rejectedRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, rejectedRefresh.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenReplay_RevokesRotatedTokenFamily()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);
        using var login = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var first = await ReadTokensAsync(login);

        using var rotation = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        var rotated = await ReadTokensAsync(rotation);

        using var replay = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var revokedDescendant = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, revokedDescendant.StatusCode);
    }

    [Fact]
    public async Task LogoutEverywhere_RevokesAllRefreshTokensForTheAuthenticatedUser()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);
        var first = await LoginAsync(client, account.Username);
        var second = await LoginAsync(client, account.Email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);

        using var logout = await client.PostAsync("/auth/logout-everywhere", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var firstRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = first.RefreshToken });
        using var secondRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, firstRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task ForgotAndResetPassword_ChangesCredentialsAndRevokesExistingRefreshTokens()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);
        using var login = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await ReadTokensAsync(login);

        using var forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email = account.Email });
        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);
        var resetToken = fixture.EmailSender.LatestToken("Password reset");
        using var reset = await client.PostAsJsonAsync("/auth/reset-password", new { email = account.Email, token = resetToken, newPassword = "NewGoodPass456!" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var oldLogin = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Email, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, oldLogin.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Email, password = "NewGoodPass456!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var oldRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, oldRefresh.StatusCode);
    }

    [Fact]
    public async Task EmailChangeAndPasswordChange_RequireConfirmationAndRevokeSessions()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);
        using var login = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Username, password = Password });
        var tokens = await ReadTokensAsync(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var newEmail = $"changed-{Guid.NewGuid():N}@example.test";
        using var changeEmail = await client.PostAsJsonAsync("/auth/change-email", new { newEmail, password = Password });
        Assert.Equal(HttpStatusCode.Accepted, changeEmail.StatusCode);
        var emailToken = fixture.EmailSender.LatestToken("Confirm your ChessPlatform email change");
        using var confirm = await client.PostAsJsonAsync("/auth/confirm-email-change", new { token = emailToken });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using var oldRefresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, oldRefresh.StatusCode);

        using var nextLogin = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = newEmail, password = Password });
        Assert.Equal(HttpStatusCode.OK, nextLogin.StatusCode);
        var current = await ReadTokensAsync(nextLogin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using var changePassword = await client.PostAsJsonAsync("/auth/change-password", new { currentPassword = Password, newPassword = "ChangedPass789!" });
        Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);
        using var oldPasswordLogin = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = newEmail, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, oldPasswordLogin.StatusCode);
        using var newPasswordLogin = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = newEmail, password = "ChangedPass789!" });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task SocialRequestsPreferencesBlocksAndLookup_FollowExpectedTransitions()
    {
        using var client = fixture.CreateClient();
        var alice = await RegisterAndVerifyAsync(client);
        var bob = await RegisterAndVerifyAsync(client);
        var aliceTokens = await LoginAsync(client, alice.Username);
        var bobTokens = await LoginAsync(client, bob.Username);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceTokens.AccessToken);
        using var defaultPreferences = await client.GetAsync("/social/preferences");
        Assert.Equal(HttpStatusCode.OK, defaultPreferences.StatusCode);
        using var updatePreferences = await client.PutAsJsonAsync("/social/preferences", new { boardTheme = "wood", pieceTheme = "neo", soundEnabled = false, premovesEnabled = true, showOnlineStatus = false, allowChallenges = true });
        Assert.Equal(HttpStatusCode.OK, updatePreferences.StatusCode);

        using var send = await client.PostAsJsonAsync("/social/friend-requests", new { userId = bob.UserId });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        using var sentDocument = await JsonDocument.ParseAsync(await send.Content.ReadAsStreamAsync());
        var requestId = sentDocument.RootElement.GetProperty("id").GetGuid();
        using var duplicate = await client.PostAsJsonAsync("/social/friend-requests", new { userId = bob.UserId });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bobTokens.AccessToken);
        using var incoming = await client.GetAsync("/social/friend-requests/incoming");
        Assert.Equal(HttpStatusCode.OK, incoming.StatusCode);
        using var accept = await client.PostAsync($"/social/friend-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceTokens.AccessToken);
        using var friends = await client.GetAsync("/social/friends");
        Assert.Equal(HttpStatusCode.OK, friends.StatusCode);
        using (var friendsJson = await JsonDocument.ParseAsync(await friends.Content.ReadAsStreamAsync()))
            Assert.Contains(friendsJson.RootElement.EnumerateArray(), user => user.GetProperty("userId").GetGuid() == bob.UserId);

        using var block = await client.PostAsync($"/social/blocks/{bob.UserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);
        using var hiddenFriends = await client.GetAsync("/social/friends");
        using (var hidden = await JsonDocument.ParseAsync(await hiddenFriends.Content.ReadAsStreamAsync()))
            Assert.Empty(hidden.RootElement.EnumerateArray());
        using var blocks = await client.GetAsync("/social/blocks");
        using (var blocked = await JsonDocument.ParseAsync(await blocks.Content.ReadAsStreamAsync()))
            Assert.Contains(blocked.RootElement.EnumerateArray(), user => user.GetProperty("userId").GetGuid() == bob.UserId);
        using var unblock = await client.DeleteAsync($"/social/blocks/{bob.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);

        using var lookup = await client.PostAsJsonAsync("/users/lookup", new { userIds = new[] { alice.UserId, bob.UserId, Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        using var lookupJson = await JsonDocument.ParseAsync(await lookup.Content.ReadAsStreamAsync());
        Assert.Equal(2, lookupJson.RootElement.GetArrayLength());
        Assert.DoesNotContain("email", lookupJson.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SocialRequests_CanBeDeclinedAcceptedAndRemoved()
    {
        using var client = fixture.CreateClient();
        var sender = await RegisterAndVerifyAsync(client);
        var receiver = await RegisterAndVerifyAsync(client);
        var senderTokens = await LoginAsync(client, sender.Username);
        var receiverTokens = await LoginAsync(client, receiver.Username);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", senderTokens.AccessToken);
        using var send = await client.PostAsJsonAsync("/social/friend-requests", new { userId = receiver.UserId });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        using var outgoing = await client.GetAsync("/social/friend-requests/outgoing");
        Assert.Equal(HttpStatusCode.OK, outgoing.StatusCode);
        using var sent = await JsonDocument.ParseAsync(await send.Content.ReadAsStreamAsync());
        var requestId = sent.RootElement.GetProperty("id").GetGuid();

        using var wrongActor = await client.PostAsync($"/social/friend-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, wrongActor.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", receiverTokens.AccessToken);
        using var accept = await client.PostAsync($"/social/friend-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // A second, independent request covers decline; acceptance above already transitioned it.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", senderTokens.AccessToken);
        using var duplicateAccepted = await client.PostAsJsonAsync("/social/friend-requests", new { userId = receiver.UserId });
        Assert.Equal(HttpStatusCode.Conflict, duplicateAccepted.StatusCode);
        using var remove = await client.DeleteAsync($"/social/friends/{receiver.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var sendAgain = await client.PostAsJsonAsync("/social/friend-requests", new { userId = receiver.UserId });
        Assert.Equal(HttpStatusCode.Created, sendAgain.StatusCode);
        using var declinedRequest = await JsonDocument.ParseAsync(await sendAgain.Content.ReadAsStreamAsync());
        var declinedId = declinedRequest.RootElement.GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", receiverTokens.AccessToken);
        using var decline = await client.PostAsync($"/social/friend-requests/{declinedId}/decline", null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", senderTokens.AccessToken);
        using var noOutgoing = await client.GetAsync("/social/friend-requests/outgoing");
        using var outgoingJson = await JsonDocument.ParseAsync(await noOutgoing.Content.ReadAsStreamAsync());
        Assert.Empty(outgoingJson.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ProfileSearchAndAccountDeletion_WorkAndHideDeletedAccount()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAndVerifyAsync(client);
        var tokens = await LoginAsync(client, account.Username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var changedUsername = $"renamed_{Guid.NewGuid():N}"[..20];
        using var update = await client.PutAsJsonAsync("/users/me/profile", new { username = changedUsername });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var search = await client.GetAsync($"/users/search?q={changedUsername[..8]}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using (var matches = await JsonDocument.ParseAsync(await search.Content.ReadAsStreamAsync()))
            Assert.Contains(matches.RootElement.EnumerateArray(), user => user.GetProperty("username").GetString() == changedUsername);
        using var publicProfile = await client.GetAsync($"/users/{changedUsername}");
        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);

        using var wrongPasswordDelete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/users/me")
        {
            Content = JsonContent.Create(new { password = "WrongPassword123!" }, options: Json)
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPasswordDelete.StatusCode);
        using var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/users/me")
        {
            Content = JsonContent.Create(new { password = Password }, options: Json)
        });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var me = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.NotFound, me.StatusCode);
    }

    [Fact]
    public async Task RegistrationRejectsDuplicateAndUnverifiedAccountsCannotLogin()
    {
        using var client = fixture.CreateClient();
        var account = await RegisterAsync(client);
        using var loginBeforeVerification = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail = account.Username, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, loginBeforeVerification.StatusCode);

        using var duplicate = await client.PostAsJsonAsync("/auth/register", new { username = account.Username, email = account.Email, password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        using var resend = await client.PostAsJsonAsync("/auth/resend-verification", new { email = account.Email });
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
        using var verify = await client.PostAsJsonAsync("/auth/verify-email", new { email = account.Email, code = fixture.EmailSender.LatestCode("verification code") });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
    }

    private async Task<Account> RegisterAndVerifyAsync(HttpClient client)
    {
        var account = await RegisterAsync(client);
        var code = fixture.EmailSender.LatestCode("verification code");
        using var verify = await client.PostAsJsonAsync("/auth/verify-email", new { email = account.Email, code });
        Assert.True(verify.StatusCode == HttpStatusCode.NoContent,
            $"Email verification returned {(int)verify.StatusCode}: {await verify.Content.ReadAsStringAsync()}");
        return account;
    }

    private static async Task<Account> RegisterAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N");
        // Registration enforces a 20-character maximum: "test_" + 15 unique characters.
        var username = $"test_{suffix[..15]}";
        var email = $"{suffix}@example.test";
        using var response = await client.PostAsJsonAsync("/auth/register", new { username, email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var userId = json.RootElement.TryGetProperty("userId", out var id) ? id.GetGuid() : json.RootElement.GetProperty("id").GetGuid();
        return new Account(userId, username, email);
    }

    private static async Task<Tokens> LoginAsync(HttpClient client, string usernameOrEmail)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { usernameOrEmail, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        return new Tokens(root.GetProperty("accessToken").GetString()!, root.GetProperty("refreshToken").GetString()!);
    }

    private sealed record Account(Guid UserId, string Username, string Email);
    private sealed record Tokens(string AccessToken, string RefreshToken);
}
