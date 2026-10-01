namespace IskBattle.Domain.Entities;

public class CheckIn
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ForDate { get; set; }      // Europe/Stockholm date; unique (UserId, ForDate)
    public int Season => ForDate.Year;
    public decimal YtdPercent { get; set; }    // decimal(9,4), range -99.99..1000
    public DateTimeOffset RecordedAt { get; set; }
}
