using Identity.Domain.Repositories;

namespace Identity.Application.Users.GetCurrentUser;

public sealed class GetCurrentUserHandler
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserHandler(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetCurrentUserResult> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            throw new Identity.Application.Common.ApplicationException(
                "user_not_found",
                "User was not found.");
        }

        return new GetCurrentUserResult(
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt,
            user.Role);
    }
}