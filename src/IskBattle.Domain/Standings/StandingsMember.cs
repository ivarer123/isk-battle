namespace IskBattle.Domain.Standings;

/// <summary>An active league member. Pending members are not passed to the calculator.</summary>
public sealed record StandingsMember(Guid UserId, string DisplayName);
