using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IskBattle.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.Property(p => p.StripeCheckoutSessionId).HasMaxLength(255);
        builder.Property(p => p.StripePaymentIntentId).HasMaxLength(255);
        builder.HasIndex(p => p.StripeCheckoutSessionId).IsUnique();
        builder.HasIndex(p => p.LeagueId);

        // Purchases are retained for 7 years (bokföringslagen): leagues are only soft-deleted,
        // and deleting the buyer only removes the reference.
        builder.HasOne<League>().WithMany().HasForeignKey(p => p.LeagueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.BuyerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
