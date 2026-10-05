using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IskBattle.Infrastructure.Persistence.Configurations;

internal sealed class JoinPendingNoticeConfiguration : IEntityTypeConfiguration<JoinPendingNotice>
{
    public void Configure(EntityTypeBuilder<JoinPendingNotice> builder)
    {
        builder.HasKey(n => new { n.LeagueId, n.UserId });
        builder.HasIndex(n => n.UserId);
        builder.HasOne<League>().WithMany().HasForeignKey(n => n.LeagueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
