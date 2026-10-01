using IskBattle.Domain.Periods;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class PeriodCalculatorTests
{
    [Theory]
    [InlineData(2026, 9, 21, 2026, 9, 21, 2026, 9, 22)] // Monday
    [InlineData(2026, 9, 24, 2026, 9, 24, 2026, 9, 25)] // Thursday
    [InlineData(2026, 9, 25, 2026, 9, 25, 2026, 9, 28)] // Friday spans the weekend
    [InlineData(2026, 9, 26, 2026, 9, 25, 2026, 9, 28)] // Saturday resolves to Friday
    [InlineData(2026, 9, 27, 2026, 9, 25, 2026, 9, 28)] // Sunday resolves to Friday
    [InlineData(2027, 1, 1, 2027, 1, 1, 2027, 1, 4)]    // 1 Jan 2027 is a Friday
    [InlineData(2027, 1, 3, 2027, 1, 1, 2027, 1, 4)]    // Sunday after it
    [InlineData(2027, 1, 4, 2027, 1, 4, 2027, 1, 5)]    // first Monday is not season start
    [InlineData(2028, 1, 1, 2028, 1, 1, 2028, 1, 4)]    // Saturday 1 Jan joins the first trading day
    [InlineData(2028, 1, 2, 2028, 1, 1, 2028, 1, 4)]
    [InlineData(2028, 1, 3, 2028, 1, 1, 2028, 1, 4)]    // Monday 3 Jan, first trading day of 2028
    [InlineData(2026, 12, 31, 2026, 12, 31, 2027, 1, 1)] // Thursday clipped at season end
    [InlineData(2027, 12, 31, 2027, 12, 31, 2028, 1, 1)] // Friday clipped at season end
    public void Day(int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
    {
        var period = PeriodCalculator.For(PeriodType.Day, D(y, m, d));

        Assert.Equal(new Period(PeriodType.Day, D(sy, sm, sd), D(ey, em, ed)), period);
    }

    [Theory]
    [InlineData(2026, 9, 21, 2026, 9, 21, 2026, 9, 28)]  // Monday
    [InlineData(2026, 9, 27, 2026, 9, 21, 2026, 9, 28)]  // Sunday
    [InlineData(2026, 12, 28, 2026, 12, 28, 2027, 1, 1)] // 2026-W53, 2026 side
    [InlineData(2026, 12, 31, 2026, 12, 28, 2027, 1, 1)]
    [InlineData(2027, 1, 1, 2027, 1, 1, 2027, 1, 4)]     // 2026-W53, 2027 side
    [InlineData(2027, 1, 3, 2027, 1, 1, 2027, 1, 4)]
    [InlineData(2027, 1, 4, 2027, 1, 4, 2027, 1, 11)]    // 2027-W01
    public void Week_is_iso_and_clipped_to_season(int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
    {
        var period = PeriodCalculator.For(PeriodType.Week, D(y, m, d));

        Assert.Equal(new Period(PeriodType.Week, D(sy, sm, sd), D(ey, em, ed)), period);
    }

    [Theory]
    [InlineData(2026, 9, 21, 2026, 9, 1, 2026, 10, 1)]
    [InlineData(2026, 12, 31, 2026, 12, 1, 2027, 1, 1)]
    [InlineData(2028, 2, 29, 2028, 2, 1, 2028, 3, 1)]
    public void Month(int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
    {
        var period = PeriodCalculator.For(PeriodType.Month, D(y, m, d));

        Assert.Equal(new Period(PeriodType.Month, D(sy, sm, sd), D(ey, em, ed)), period);
    }

    [Fact]
    public void Year_is_the_calendar_year()
    {
        var period = PeriodCalculator.For(PeriodType.Year, D(2026, 9, 21));

        Assert.Equal(new Period(PeriodType.Year, D(2026, 1, 1), D(2027, 1, 1)), period);
        Assert.Equal(2026, period.Season);
    }

    [Fact]
    public void Unknown_period_type_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PeriodCalculator.For((PeriodType)99, D(2026, 1, 1)));

    [Theory]
    [InlineData(PeriodType.Day, 2026, 1, 1, true)]
    [InlineData(PeriodType.Day, 2026, 1, 2, false)]
    [InlineData(PeriodType.Day, 2028, 1, 3, true)]   // no earlier trading day in 2028
    [InlineData(PeriodType.Week, 2026, 1, 4, true)]  // 2026-W01 starts Mon 29 Dec 2025, clipped to 1 Jan
    [InlineData(PeriodType.Week, 2026, 1, 5, false)]
    [InlineData(PeriodType.Week, 2028, 1, 1, true)]  // 2027-W52 clipped to [1 Jan, 3 Jan), no trading days
    [InlineData(PeriodType.Week, 2028, 1, 5, true)]  // week of Mon 3 Jan 2028: no earlier trading day
    [InlineData(PeriodType.Week, 2028, 1, 10, false)]
    [InlineData(PeriodType.Month, 2026, 1, 31, true)]
    [InlineData(PeriodType.Month, 2026, 2, 1, false)]
    [InlineData(PeriodType.Year, 2026, 6, 1, true)]
    public void StartsSeason(PeriodType type, int y, int m, int d, bool expected) =>
        Assert.Equal(expected, PeriodCalculator.For(type, D(y, m, d)).StartsSeason);

    [Fact]
    public void Contains_is_half_open()
    {
        var week = PeriodCalculator.For(PeriodType.Week, D(2026, 9, 21));

        Assert.False(week.Contains(D(2026, 9, 20)));
        Assert.True(week.Contains(D(2026, 9, 21)));
        Assert.True(week.Contains(D(2026, 9, 27)));
        Assert.False(week.Contains(D(2026, 9, 28)));
    }

    [Theory]
    [InlineData(2026, 9, 25, true)]
    [InlineData(2026, 9, 26, false)]
    [InlineData(2026, 9, 27, false)]
    public void IsTradingDay(int y, int m, int d, bool expected) =>
        Assert.Equal(expected, PeriodCalculator.IsTradingDay(D(y, m, d)));

    [Theory]
    [InlineData(PeriodType.Day)]
    [InlineData(PeriodType.Week)]
    [InlineData(PeriodType.Month)]
    public void Periods_partition_each_season_without_gaps(PeriodType type)
    {
        foreach (var year in new[] { 2026, 2027, 2028 })
        {
            var cursor = D(year, 1, 1);
            while (cursor.Year == year)
            {
                var period = PeriodCalculator.For(type, cursor);
                Assert.Equal(cursor, period.Start);
                for (var d = period.Start; d < period.End; d = d.AddDays(1))
                    Assert.Equal(period, PeriodCalculator.For(type, d));
                cursor = period.End;
            }
            Assert.Equal(D(year + 1, 1, 1), cursor);
        }
    }
}
