using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IskBattle.Infrastructure.Persistence.Configurations;

internal sealed class LeagueMemberConfiguration : IEntityTypeConfiguration<LeagueMember>
{
    public void Configure(EntityTypeBuilder<LeagueMember> builder)
    {
        builder.HasKey(m => new { m.LeagueId, m.UserId });
        builder.HasIndex(m => new { m.LeagueId, m.Status, m.JoinedAt });
        builder.HasIndex(m => m.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
