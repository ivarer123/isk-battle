using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IskBattle.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.ExternalId).HasMaxLength(128);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.DisplayName).HasMaxLength(50);
        builder.Property(u => u.LastReminderWindowKey).HasMaxLength(16);
        builder.HasIndex(u => u.ExternalId).IsUnique();
    }
}
