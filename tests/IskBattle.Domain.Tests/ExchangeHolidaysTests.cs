using IskBattle.Domain.Periods;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class ExchangeHolidaysTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2038, 4, 25)] // latest possible date
    [InlineData(2285, 3, 22)] // earliest possible date
    public void Easter_sunday(int y, int m, int d) =>
        Assert.Equal(D(y, m, d), ExchangeHolidays.EasterSunday(y));

    [Fact]
    public void Nasdaq_stockholm_closed_days_2026() =>
        // Nyårsdagen, Trettondedag jul, Långfredagen, Annandag påsk, 1 maj, Kristi himmelsfärdsdag, nationaldagen,
        // Midsommarafton, Julafton, Juldagen, Annandag jul (Saturday), Nyårsafton.
        Assert.Equal(
            [D(2026, 1, 1), D(2026, 1, 6), D(2026, 4, 3), D(2026, 4, 6), D(2026, 5, 1), D(2026, 5, 14), D(2026, 6, 6),
             D(2026, 6, 19), D(2026, 12, 24), D(2026, 12, 25), D(2026, 12, 26), D(2026, 12, 31)],
            Enumerable.Range(0, 365).Select(i => D(2026, 1, 1).AddDays(i)).Where(ExchangeHolidays.IsHoliday));

    [Theory]
    [InlineData(2026, 6, 19, true)]  // Midsommarafton is the Friday 19–25 June
    [InlineData(2027, 6, 25, true)]
    [InlineData(2027, 6, 18, false)]
    [InlineData(2026, 6, 26, false)]
    [InlineData(2026, 4, 2, false)]  // Skärtorsdagen is a (half) trading day
    [InlineData(2026, 5, 15, false)] // the day after Kristi himmelsfärd
    [InlineData(2026, 12, 23, false)]
    [InlineData(2026, 3, 16, false)]
    public void Is_holiday(int y, int m, int d, bool expected) =>
        Assert.Equal(expected, ExchangeHolidays.IsHoliday(D(y, m, d)));

    [Fact]
    public void Holidays_are_not_trading_days() =>
        Assert.Equal((false, true), (PeriodCalculator.IsTradingDay(D(2026, 4, 6)), PeriodCalculator.IsTradingDay(D(2026, 4, 7))));
}
