using System.Reflection;
using Delivery.Delivery.Models;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Data;

public class DeliveryDbContext: DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options) { }
    
    public DbSet<GpsDeliveryH> GpsDeliveryHs => Set<GpsDeliveryH>();
    public DbSet<GpsDeliveryD> GpsDeliveryDs => Set<GpsDeliveryD>();
    public DbSet<DeliveryProgress> DeliveryProgresses => Set<DeliveryProgress>();
    public DbSet<GpsDelivery> GpsDeliveries => Set<GpsDelivery>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");
        
        builder.Entity<DeliveryProgress>(entity =>
        {
            entity.ToTable("tb_r_delivery_progress");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
        
        builder.Entity<GpsDelivery>(entity =>
        {
            entity.ToTable("tb_r_gps_delivery");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);

    }
    
    
}