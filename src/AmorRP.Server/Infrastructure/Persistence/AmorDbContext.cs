using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class AmorDbContext(DbContextOptions<AmorDbContext> options) : DbContext(options)
{
    public DbSet<InfrastructureState> InfrastructureStates => Set<InfrastructureState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfrastructureState>(entity =>
        {
            entity.ToTable("infrastructure_state");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasMaxLength(40);
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            entity.HasData(new InfrastructureState { Id = "bootstrap", SchemaVersion = 1 });
        });
    }
}

public sealed class InfrastructureState
{
    public required string Id { get; set; }
    public int SchemaVersion { get; set; }
}
