namespace IskBattle.Domain.Periods;

/// <summary>
/// Weekdays when Nasdaq Stockholm is closed (GAME-DESIGN.md §5), computed from rules so no list needs upkeep:
/// Nyårsdagen, Trettondedag jul, Långfredagen, Annandag påsk, 1 maj, Kristi himmelsfärdsdag, Sveriges
/// nationaldag, Midsommarafton, Julafton, Juldagen, Annandag jul and Nyårsafton. Half days are normal trading days.
/// Uses today's rules for every year (the national day replaced Annandag pingst in 2005).
/// </summary>
public static class ExchangeHolidays
{
    public static bool IsHoliday(DateOnly date) => date switch
    {
        { Month: 1, Day: 1 or 6 } => true,
        { Month: 5, Day: 1 } => true,
        { Month: 6, Day: 6 } => true,
        { Month: 6, Day: >= 19 and <= 25, DayOfWeek: DayOfWeek.Friday } => true, // Midsommarafton
        { Month: 12, Day: 24 or 25 or 26 or 31 } => true,
        { Month: >= 3 and <= 6 } => IsEasterBased(date),
        _ => false,
    };

    private static bool IsEasterBased(DateOnly date)
    {
        var offset = date.DayNumber - EasterSunday(date.Year).DayNumber;
        return offset is -2 or 1 or 39; // Långfredagen, Annandag påsk, Kristi himmelsfärdsdag
    }

    /// <summary>Gregorian Easter Sunday (anonymous Gregorian algorithm, a.k.a. Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
