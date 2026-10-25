namespace Identity.Application.Abstractions;

public interface IEmailSender
{
    Task SendEmailAsync(string recipientEmail, string subject, string htmlContent, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default);
}
