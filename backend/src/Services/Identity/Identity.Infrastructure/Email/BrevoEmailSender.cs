using Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Identity.Infrastructure.Email;

public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _options;

    public BrevoEmailSender(
        HttpClient httpClient,
        IOptions<BrevoOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        var request = new { sender = new { name = _options.SenderName, email = _options.SenderEmail }, to = new[] { new { email = recipientEmail } }, subject, htmlContent };
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
        requestMessage.Headers.Add("api-key", _options.ApiKey);
        requestMessage.Content = JsonContent.Create(request);
        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo email request failed with status {(int)response.StatusCode}: {responseBody}");
        }
    }

    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var resetUrl =
            $"{_options.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
            $"?token={Uri.EscapeDataString(resetToken)}" +
            $"&email={Uri.EscapeDataString(recipientEmail)}";

        var encodedResetUrl =
            System.Net.WebUtility.HtmlEncode(resetUrl);

        var request = new
        {
            sender = new
            {
                name = _options.SenderName,
                email = _options.SenderEmail
            },
            to = new[]
            {
                new
                {
                    email = recipientEmail
                }
            },
            subject = "Reset your ChessPlatform password",
            htmlContent = $"""
                <html>
                <body>
                    <h2>Password reset</h2>

                    <p>
                        We received a request to reset your
                        ChessPlatform password.
                    </p>

                    <p>
                        <a href="{encodedResetUrl}">
                            Reset your password
                        </a>
                    </p>

                    <p>
                        This link expires in 30 minutes and can
                        only be used once.
                    </p>

                    <p>
                        If you did not request this, you can
                        safely ignore this email.
                    </p>
                </body>
                </html>
                """
        };

        using var requestMessage =
            new HttpRequestMessage(
                HttpMethod.Post,
                "v3/smtp/email");

        requestMessage.Headers.Add(
            "api-key",
            _options.ApiKey);

        requestMessage.Content =
            JsonContent.Create(request);

        using var response =
            await _httpClient.SendAsync(
                requestMessage,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                $"Brevo email request failed with status " +
                $"{(int)response.StatusCode}: " +
                $"{responseBody}");
        }
    }
}
