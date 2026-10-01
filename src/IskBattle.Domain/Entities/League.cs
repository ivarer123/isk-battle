namespace IskBattle.Domain.Entities;

public class League
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Cadence Cadence { get; set; }
    public Cadence? NextSeasonCadence { get; set; }
    public LeagueTier Tier { get; set; }
    public string InviteCode { get; set; } = "";   // 8 chars, unambiguous alphabet, unique
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public List<LeagueMember> Members { get; set; } = [];
}
