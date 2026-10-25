namespace Game.Domain.Entities;

public sealed class GameChallenge
{
    public Guid Id { get; set; }
    public Guid ChallengerUserId { get; set; }
    public Guid OpponentUserId { get; set; }
    public int InitialTimeSeconds { get; set; }
    public int IncrementSeconds { get; set; }
    public bool Rated { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? GameId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}
