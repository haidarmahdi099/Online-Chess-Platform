namespace Identity.Infrastructure.Email;

public sealed class BrevoOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "ChessPlatform";

    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
}