using Identity.Domain.Repositories;

namespace Identity.Application.Users.LookupUsers;

public sealed class LookupUsersHandler
{
    public const int MaxIds = 100;

    private readonly IUserRepository _userRepository;

    public LookupUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LookupUsersResult> HandleAsync(
        IReadOnlyCollection<Guid>? userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = (userIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            throw new Identity.Application.Common.ApplicationException(
                "user_ids_required",
                "At least one user ID is required.");
        }

        if (ids.Length > MaxIds)
        {
            throw new Identity.Application.Common.ApplicationException(
                "too_many_user_ids",
                $"At most {MaxIds} user IDs can be looked up at once.");
        }

        var users = await _userRepository.GetByIdsAsync(ids, cancellationToken);

        return new LookupUsersResult(
            users.Select(user => new LookupUserItem(user.Id, user.Username)).ToList());
    }
}
