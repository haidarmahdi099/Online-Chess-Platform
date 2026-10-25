namespace Game.Application.Exceptions;

public sealed class GameSeekNotFoundException : Exception
{
    public GameSeekNotFoundException()
        : base("You do not have an active game search.")
    {
    }
}