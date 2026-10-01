using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IskBattle.Infrastructure.Persistence.Configurations;

internal sealed class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(60);
        builder.Property(l => l.InviteCode).HasMaxLength(8).IsFixedLength();
        builder.HasIndex(l => l.InviteCode).IsUnique();

        // Optimistic concurrency for tier changes and membership updates.
        builder.Property<byte[]>("Version").IsRowVersion();

        // Soft delete: deleted leagues are invisible unless IgnoreQueryFilters() is used.
        builder.HasQueryFilter(l => l.DeletedAt == null);

        builder.HasMany(l => l.Members).WithOne().HasForeignKey(m => m.LeagueId).OnDelete(DeleteBehavior.Cascade);
    }
}
