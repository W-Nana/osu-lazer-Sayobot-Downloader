using System.Globalization;

namespace osu.Game.Rulesets.Sayobot.Download;

internal static class BeatmapSetIdParser
{
    public static bool TryParse(string text, out int id)
    {
        text = text.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("osu.ppy.sh", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == "beatmapsets")
                text = parts[1];
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }
}
