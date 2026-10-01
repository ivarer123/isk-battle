namespace IskBattle.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";      // Entra 'sub'
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool EmailRemindersEnabled { get; set; } = true;
    public string? LastReminderWindowKey { get; set; } // e.g. "D:2026-09-21", "W:2026-38", "M:2026-09"
    public DateTimeOffset CreatedAt { get; set; }
}
