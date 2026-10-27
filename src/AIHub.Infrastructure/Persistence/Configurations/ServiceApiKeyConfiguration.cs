using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIHub.Infrastructure.Persistence.Configurations;

internal sealed class ServiceApiKeyConfiguration : IEntityTypeConfiguration<ServiceApiKey>
{
    public void Configure(EntityTypeBuilder<ServiceApiKey> builder)
    {
        builder.ToTable("service_api_keys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(k => k.ServiceName).HasColumnName("service_name").IsRequired();
        builder.Property(k => k.KeyHash).HasColumnName("key_hash").IsRequired();
        builder.Property(k => k.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(k => k.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(k => k.KeyHash).IsUnique().HasDatabaseName("ux_service_api_keys_key_hash");
    }
}
