namespace Identity.Application.Common;

public static class PasswordValidator
{
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ApplicationException(
                "password_invalid",
                "Password is required.");
        }

        if (password.Length < 10)
        {
            throw new ApplicationException(
                "password_invalid",
                "Password must be at least 10 characters long.");
        }

        var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password123", "password1234", "1234567890", "qwerty1234",
            "letmein123", "admin12345", "welcome123"
        };

        if (commonPasswords.Contains(password))
        {
            throw new ApplicationException(
                "password_invalid",
                "Choose a less common password.");
        }

        if (!password.Any(char.IsLetter))
        {
            throw new ApplicationException(
                "password_invalid",
                "Password must contain at least one letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new ApplicationException(
                "password_invalid",
                "Password must contain at least one digit.");
        }
    }
}