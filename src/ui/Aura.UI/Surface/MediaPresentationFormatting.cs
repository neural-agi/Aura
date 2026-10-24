using System.Globalization;

namespace Aura.UI.Surface;

public static class MediaPresentationFormatting
{
    public static string FormatVolumeValue(double? normalizedLevel)
    {
        if (normalizedLevel is not { } level || !double.IsFinite(level)) return "Unavailable";

        var percentage = Math.Round(Math.Clamp(level, 0d, 1d) * 100d, MidpointRounding.AwayFromZero);
        return percentage.ToString("0", CultureInfo.InvariantCulture);
    }

    public static string FormatSourceName(string? sourceApplication)
    {
        if (string.IsNullOrWhiteSpace(sourceApplication)) return "Media";

        var value = sourceApplication.Trim();
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];
        else if (value.Contains('!'))
            value = value[(value.LastIndexOf('!') + 1)..];

        return string.IsNullOrWhiteSpace(value) ? sourceApplication : value;
    }
}
