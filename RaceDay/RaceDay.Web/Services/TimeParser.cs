using System.Globalization;

namespace RaceDay.Web.Services;

public static class TimeParser
{
    /// <summary>Reads a finish time typed as h:mm:ss or m:ss, for example "1:47:32" or "47:05".</summary>
    public static bool TryParseFinishTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Trim().Split(':');
        if (parts.Length is < 2 or > 3)
            return false;

        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        int hours = 0, minutes, seconds;
        if (parts.Length == 3)
        {
            hours = numbers[0];
            minutes = numbers[1];
            seconds = numbers[2];
            if (hours > 99 || minutes > 59)
                return false;
        }
        else
        {
            minutes = numbers[0];
            seconds = numbers[1];
            if (minutes > 999)
                return false;
        }

        if (seconds > 59)
            return false;

        time = new TimeSpan(hours, minutes, seconds);
        return time > TimeSpan.Zero;
    }
}