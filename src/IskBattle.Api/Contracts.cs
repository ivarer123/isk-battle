using IskBattle.Domain;
using IskBattle.Domain.Periods;
using IskBattle.Domain.Standings;

namespace IskBattle.Api;

public sealed record MeResponse(
    Guid Id,
    string DisplayName,
    string Email,
    bool EmailRemindersEnabled,
    Cadence? EffectiveCadence,
    DateOnly Today,
    CheckInResponse? LastCheckIn);

public sealed record UpdateMeRequest(string? DisplayName, bool? EmailRemindersEnabled);

public sealed record CheckInRequest(decimal YtdPercent, bool Confirmed);

public sealed record CheckInResponse(DateOnly ForDate, decimal YtdPercent);

public sealed record CreateLeagueRequest(string Name, Cadence Cadence);

public sealed record UpdateLeagueRequest(string? Name, Cadence? NextSeasonCadence);

public sealed record JoinLeagueRequest(string InviteCode);

public sealed record JoinLeagueResponse(Guid LeagueId, string LeagueName, MemberStatus Status, bool AlreadyMember);

public sealed record LeagueSummary(
    Guid Id,
    string Name,
    Cadence Cadence,
    LeagueTier Tier,
    MemberRole MyRole,
    MemberStatus MyStatus,
    int ActiveMemberCount,
    int Capacity);

public sealed record LeagueMemberResponse(Guid UserId, string DisplayName, MemberRole Role, MemberStatus Status);

public sealed record UpgradeOption(LeagueTier Tier, int Capacity, int PriceOre);

/// <param name="InviteCode">Only for hosts.</param>
/// <param name="Members">Empty for Pending viewers. Hosts also see Pending members.</param>
public sealed record LeagueDetail(
    Guid Id,
    string Name,
    Cadence Cadence,
    Cadence? NextSeasonCadence,
    LeagueTier Tier,
    int Capacity,
    int ActiveMemberCount,
    int PendingMemberCount,
    MemberRole MyRole,
    MemberStatus MyStatus,
    string? InviteCode,
    IReadOnlyList<PeriodType> Periods,
    IReadOnlyList<LeagueMemberResponse> Members,
    IReadOnlyList<UpgradeOption> UpgradeOptions);

/// <param name="End">Exclusive.</param>
/// <param name="IsoWeek">ISO week number for week periods.</param>
public sealed record PeriodResponse(PeriodType Type, DateOnly Start, DateOnly End, int Season, int? IsoWeek);

public sealed record StandingsRowResponse(
    Guid UserId,
    string DisplayName,
    StandingStatus Status,
    int? Rank,
    decimal? PeriodReturn,
    decimal? YtdPercent,
    bool IsMe);

public sealed record StandingsResponse(PeriodResponse Period, IReadOnlyList<StandingsRowResponse> Rows);

public sealed record StartCheckoutRequest(LeagueTier TargetTier, bool ImmediateDeliveryConsent);

public sealed record CheckoutResponse(string CheckoutUrl);
