using System.Text.Json;
using System.Text.RegularExpressions;

namespace Admin.WebApi.Models
{
    public class SiteThemeDto
    {
        public string Accent { get; set; } = "#818cf8";
        public string Background { get; set; } = "#080a10";
        public string Surface { get; set; } = "#0d1220";
        public string Text { get; set; } = "#f1f5f9";
    }

    public static class SiteThemeStore
    {
        private static readonly Regex HexColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static SiteThemeDto Defaults() => new SiteThemeDto();

        public static SiteThemeDto Normalize(SiteThemeDto? theme)
        {
            var defaults = Defaults();

            return new SiteThemeDto
            {
                Accent = IsValidHex(theme?.Accent) ? theme!.Accent : defaults.Accent,
                Background = IsValidHex(theme?.Background) ? theme!.Background : defaults.Background,
                Surface = IsValidHex(theme?.Surface) ? theme!.Surface : defaults.Surface,
                Text = IsValidHex(theme?.Text) ? theme!.Text : defaults.Text
            };
        }

        public static SiteThemeDto? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<SiteThemeDto>(json, SerializerOptions);
            }
            catch
            {
                return null;
            }
        }

        public static string Serialize(SiteThemeDto theme) => JsonSerializer.Serialize(theme, SerializerOptions);

        public static bool IsValidHex(string? value) => value != null && HexColorPattern.IsMatch(value);
    }
}
