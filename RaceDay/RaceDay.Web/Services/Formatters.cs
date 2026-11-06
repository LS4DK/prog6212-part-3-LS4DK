using System.Globalization;

namespace RaceDay.Web.Services;

/// <summary>Turns raw values from the API into the text shown on the pages.</summary>
public static class Formatters
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13)
            return $"{number}th";

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    /// <summary>"47th out of 312 finishers"</summary>
    public static string Position(int? position, int totalFinishers)
    {
        if (!position.HasValue)
            return "-";

        var finishers = totalFinishers == 1 ? "finisher" : "finishers";
        return totalFinishers > 0
            ? $"{Ordinal(position.Value)} out of {totalFinishers} {finishers}"
            : Ordinal(position.Value);
    }

    /// <summary>"1:47:32" (or "47:05" for times under an hour)</summary>
    public static string FinishTime(TimeSpan? time)
    {
        if (!time.HasValue)
            return "-";

        return time.Value.TotalHours >= 1
            ? time.Value.ToString(@"h\:mm\:ss", Culture)
            : time.Value.ToString(@"m\:ss", Culture);
    }

    /// <summary>"Sun, 6 Dec 2026, 07:00" (the time is left out when it is midnight)</summary>
    public static string EventDate(DateTime date) =>
        date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("ddd, d MMM yyyy", Culture)
            : date.ToString("ddd, d MMM yyyy, HH:mm", Culture);

    public static string ShortDate(DateTime date) => date.ToString("d MMM yyyy", Culture);

    public static string Distance(decimal km) => $"{km.ToString("0.##", Culture)} km";
}