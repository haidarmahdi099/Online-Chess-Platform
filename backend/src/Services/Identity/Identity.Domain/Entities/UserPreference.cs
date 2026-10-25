namespace Identity.Domain.Entities;

public sealed class UserPreference
{
    public Guid UserId { get; set; }
    public string BoardTheme { get; set; } = "classic";
    public string PieceTheme { get; set; } = "standard";
    public bool SoundEnabled { get; set; } = true;
    public bool PremovesEnabled { get; set; } = true;
    public bool ShowOnlineStatus { get; set; } = true;
    public bool AllowChallenges { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
