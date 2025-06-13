using Microsoft.EntityFrameworkCore;

namespace Ping.Data;

public class PingDbContext : DbContext
{
    public PingDbContext(DbContextOptions<PingDbContext> options)
        : base(options) { }

    
    public DbSet<Ping.Models.Ping> Pings => Set<Ping.Models.Ping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");
        
        builder.Entity<Ping.Models.Ping>(entity =>
        {
            entity.ToTable("tb_m_ping");
            entity.Property(e => e.PingDate)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
    }
}