using Healthy.Healthy.Models;
using Microsoft.EntityFrameworkCore;

namespace Healthy.Data;

public class PingDbContext : DbContext
{
    public PingDbContext(DbContextOptions<PingDbContext> options)
        : base(options) { }
    // tb_m_ping
    
    public DbSet<Ping> Pings => Set<Ping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");
        
        builder.Entity<Ping>(entity =>
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