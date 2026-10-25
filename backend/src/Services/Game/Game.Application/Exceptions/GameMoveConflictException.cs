namespace Game.Application.Exceptions;

public sealed class GameMoveConflictException : Exception
{
    public GameMoveConflictException()
        : base("The move could not be saved because the game was updated by another request.")
    {
    }
}