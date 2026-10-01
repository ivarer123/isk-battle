namespace IskBattle.Domain.Standings;

public enum StandingStatus { Ranked, Ny, Pausad }

/// <param name="Rank">Competition rank (1, 1, 3). Null unless <see cref="StandingStatus.Ranked"/>.</param>
/// <param name="PeriodReturn">Return for the period in %, rounded. Null unless ranked.</param>
/// <param name="YtdPercent">Last known YTD within the season up to the period end, or null if none.</param>
public sealed record StandingsRow(
    Guid UserId,
    string DisplayName,
    StandingStatus Status,
    int? Rank,
    decimal? PeriodReturn,
    decimal? YtdPercent);
