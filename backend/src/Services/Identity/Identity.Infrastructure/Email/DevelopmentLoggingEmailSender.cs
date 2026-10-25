using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Email;

/// <summary>
/// Development-only replacement for Brevo when no API key is configured: writes the reset link
/// to the log so the forgot-password flow can be exercised end to end without a mail provider.
/// It is never registered outside the Development environment.
/// </summary>
public sealed class DevelopmentLoggingEmailSender : IEmailSender
{
    private readonly ILogger<DevelopmentLoggingEmailSender> _logger;
    private readonly BrevoOptions _options;

    public DevelopmentLoggingEmailSender(
        ILogger<DevelopmentLoggingEmailSender> logger,
        IOptions<BrevoOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendEmailAsync(string recipientEmail, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DEVELOPMENT ONLY - email to {Email}, subject {Subject}: {Content}", recipientEmail, subject, htmlContent);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var resetUrl =
            $"{_options.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
            $"?token={Uri.EscapeDataString(resetToken)}" +
            $"&email={Uri.EscapeDataString(recipientEmail)}";

        _logger.LogWarning(
            "DEVELOPMENT ONLY - password reset link for {Email}: {ResetUrl}",
            recipientEmail,
            resetUrl);

        return Task.CompletedTask;
    }
}
