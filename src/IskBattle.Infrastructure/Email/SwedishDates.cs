using System.Globalization;

namespace IskBattle.Infrastructure.Email;

/// <summary>
/// Swedish date texts for emails, matching <c>formatStockholmDateTime</c> in the web app. Names are spelled out
/// here so the output doesn't depend on the host's ICU data.
/// </summary>
public static class SwedishDates
{
    private static readonly string[] Weekdays = ["söndag", "måndag", "tisdag", "onsdag", "torsdag", "fredag", "lördag"];

    private static readonly string[] Months =
        ["januari", "februari", "mars", "april", "maj", "juni", "juli", "augusti", "september", "oktober", "november", "december"];

    /// <summary>
    /// A Stockholm local time relative to <paramref name="today"/>: "ikväll kl. 22.30", "idag kl. 09.00",
    /// "imorgon kl. 09.00" or "måndag 2 november kl. 09.00". Midnight is shown as 23.59 the day before, matching
    /// how the rules are written ("Sunday 23:59").
    /// </summary>
    public static string Relative(DateTime stockholmTime, DateOnly today)
    {
        var at = stockholmTime.TimeOfDay == TimeSpan.Zero ? stockholmTime.AddMinutes(-1) : stockholmTime;
        var date = DateOnly.FromDateTime(at);
        var time = string.Create(CultureInfo.InvariantCulture, $"kl. {at:HH}.{at:mm}");
        if (date == today)
            return $"{(at.Hour >= 18 ? "ikväll" : "idag")} {time}";
        if (date == today.AddDays(1))
            return $"imorgon {time}";
        return $"{Weekdays[(int)date.DayOfWeek]} {date.Day} {Months[date.Month - 1]} {time}";
    }
}
