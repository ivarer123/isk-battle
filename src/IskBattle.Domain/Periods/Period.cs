namespace IskBattle.Domain.Periods;

/// <summary>
/// A half-open date range <c>[Start, End)</c> in Europe/Stockholm dates. Always lies within a single season.
/// </summary>
public sealed record Period(PeriodType Type, DateOnly Start, DateOnly End)
{
    public int Season => Start.Year;

    public DateOnly SeasonStart => new(Season, 1, 1);

    /// <summary>
    /// True when the season has no trading day before <see cref="Start"/>, so members without a base
    /// check-in use <c>base = 0</c> instead of being shown as "Ny" (GAME-DESIGN.md §6).
    /// For example, when 1–2 January fall on a weekend, the week starting Monday 3 January also starts the season.
    /// </summary>
    public bool StartsSeason
    {
        get
        {
            for (var date = SeasonStart; date < Start; date = date.AddDays(1))
            {
                if (PeriodCalculator.IsTradingDay(date))
                    return false;
            }
            return true;
        }
    }

    public bool Contains(DateOnly date) => date >= Start && date < End;
}
