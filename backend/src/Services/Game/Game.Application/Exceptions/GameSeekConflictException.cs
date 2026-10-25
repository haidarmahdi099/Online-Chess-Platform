namespace Game.Application.Exceptions;

public sealed class GameSeekConflictException : Exception
{
    public GameSeekConflictException()
        : base("You already have an active game search.")
    {
    }
}