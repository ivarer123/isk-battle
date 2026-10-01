namespace IskBattle.Domain;

public enum Cadence { Daily, Weekly, Monthly }

public enum LeagueTier { Free, Premium50, Premium100, Premium250 }

public enum MemberRole { Owner, CoHost, Member }

public enum MemberStatus { Active, Pending }

public enum PurchaseStatus { Created, Paid, Failed, NeedsRefund }

/// <summary>Standings periods (GAME-DESIGN.md §6): Dag, Vecka, Månad, År.</summary>
public enum PeriodType { Day, Week, Month, Year }
