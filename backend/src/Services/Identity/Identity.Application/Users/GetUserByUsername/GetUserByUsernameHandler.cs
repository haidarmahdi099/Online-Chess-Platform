using Identity.Domain.Repositories;

namespace Identity.Application.Users.GetUserByUsername;

public sealed class GetUserByUsernameHandler
{
    private readonly IUserRepository _userRepository;

    public GetUserByUsernameHandler(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserByUsernameResult> HandleAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new Identity.Application.Common.ApplicationException(
                "username_required",
                "Username is required.");
        }

        var user = await _userRepository.GetByUsernameAsync(
            normalizedUsername,
            cancellationToken);

        if (user is null)
        {
            throw new Identity.Application.Common.ApplicationException(
                "user_not_found",
                "User was not found.");
        }

        return new GetUserByUsernameResult(
            user.Id,
            user.Username,
            user.CreatedAt);
    }
}