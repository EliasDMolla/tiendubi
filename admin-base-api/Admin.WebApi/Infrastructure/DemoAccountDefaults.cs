using Admin.Entities.Entities;

namespace Admin.WebApi.Infrastructure;

public static class DemoAccountDefaults
{
    public const string Email = "demo@capturar.app";
    public const string LegacyEmail = "demo1802";
    public const string Password = "DemoCapturar2026!";
    public const string PublicSlug = "demo1802";
    public const string FullName = "Demo Sports Studio";
    public const string ReadOnlyClaimType = "capturar:readonly";

    public static bool MatchesEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return string.Equals(email.Trim(), Email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(email.Trim(), LegacyEmail, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReadOnlyUser(User user)
    {
        return MatchesEmail(user.Email) || string.Equals(user.PublicSlug, PublicSlug, StringComparison.OrdinalIgnoreCase);
    }
}