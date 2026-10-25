using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Net;

namespace Identity.Application.Auth.VerifyEmail;

public static class EmailVerificationCode
{
    public const int Length = 6;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static string Hash(string email, string code, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Jwt:SecretKey must be configured to protect email verification codes.");

        var message = Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message)).ToLowerInvariant();
    }

    public static string CreateEmailHtml(string code) => $"""
        <!doctype html>
        <html lang="en">
          <body style="margin:0;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#17202a;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:32px 12px;">
              <tr><td align="center">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:480px;background:#ffffff;border-radius:12px;">
                  <tr><td style="padding:32px 28px;text-align:center;">
                    <h1 style="margin:0 0 12px;font-size:24px;">Verify your email</h1>
                    <p style="margin:0 0 24px;color:#596579;line-height:1.5;">Enter this one-time code to finish creating your ChessPlatform account.</p>
                    <div aria-label="Your verification code" style="display:inline-block;padding:16px 20px;border-radius:8px;background:#f2f4f7;color:#17202a;font-family:Consolas,'Courier New',monospace;font-size:32px;font-weight:700;letter-spacing:8px;user-select:all;">{WebUtility.HtmlEncode(code)}</div>
                    <p style="margin:24px 0 0;color:#596579;line-height:1.5;">This code expires in 10 minutes and can only be used once.</p>
                    <p style="margin:16px 0 0;color:#8490a0;font-size:13px;line-height:1.5;">If you didn’t request this code, you can ignore this email.</p>
                  </td></tr>
                </table>
              </td></tr>
            </table>
          </body>
        </html>
        """;
}
