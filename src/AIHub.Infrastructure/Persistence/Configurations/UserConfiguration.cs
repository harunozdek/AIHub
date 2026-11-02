using AIHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIHub.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(UserLimits.UserNameMaxLength).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(UserLimits.UserNameMaxLength).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(UserLimits.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count").IsRequired();
        builder.Property(u => u.LockedUntil).HasColumnName("locked_until");
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.DeactivatedAt).HasColumnName("deactivated_at");
        builder.Property(u => u.DeactivatedBy).HasColumnName("deactivated_by");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");

        // Büyük-küçük harf duyarsız tekillik normalize ad üzerinden (Spec 0004, R-7).
        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ux_users_normalized_user_name");
    }
}
