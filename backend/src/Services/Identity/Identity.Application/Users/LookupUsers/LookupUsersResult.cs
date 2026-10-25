namespace Identity.Application.Users.LookupUsers;

/// <summary>Public, non-sensitive identity of a user: just enough to show a name next to a GUID.</summary>
public sealed record LookupUserItem(Guid UserId, string Username);

public sealed record LookupUsersResult(IReadOnlyList<LookupUserItem> Users);
