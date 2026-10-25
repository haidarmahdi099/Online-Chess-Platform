namespace Game.Application.Exceptions;

public sealed class GameConcurrencyException : Exception
{
    public GameConcurrencyException()
        : base("The game was updated by another request.")
    {
    }
}